using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ES.FX.OpenData.Romania.TerritorialUnits;

namespace ES.FX.OpenData.Romania.Anaf.VatCheck.Internal;

internal sealed class AnafVatCheckClient(
    IHttpClientFactory httpClientFactory,
    string httpClientName,
    AnafRequestThrottle throttle,
    AnafVatCheckClientOptions options,
    IRomanianTerritorialUnitsDataset territorialUnits,
    AnafSirutaCrosswalk crosswalk) : IAnafVatCheckClient
{
    private const string TvaEndpoint = "api/PlatitorTvaRest/v9/tva";

    public async Task<AnafCompanyVatCheckResult?> FindCompanyAsync(long cui, DateOnly? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var batch = await FindCompaniesAsync([cui], asOf, cancellationToken).ConfigureAwait(false);
        return batch.Found.Count > 0 ? batch.Found[0] : null;
    }

    public async Task<AnafBatchCompanyVatCheckResult> FindCompaniesAsync(IReadOnlyCollection<long> cuis,
        DateOnly? asOf = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cuis);

        var found = new List<AnafCompanyVatCheckResult>();
        var notFound = new List<long>();
        if (cuis.Count == 0) return new AnafBatchCompanyVatCheckResult { Found = found, NotFound = notFound };

        using var activity = AnafVatCheckClientInstrumentation.ActivitySource.StartActivity(
            "ANAF FindCompanies", ActivityKind.Client);
        activity?.SetTag("anaf.cui_count", cuis.Count);

        try
        {
            var date = (asOf ?? DateOnly.FromDateTime(DateTime.UtcNow))
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            foreach (var chunk in cuis.Chunk(options.EffectiveBatchSize))
            {
                var requestItems = Array.ConvertAll(chunk, c => new AnafTvaRequestItem { Cui = c, Data = date });
                var requestJson = JsonSerializer.Serialize(requestItems,
                    AnafVatCheckJsonContext.Default.AnafTvaRequestItemArray);

                await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);

                var http = httpClientFactory.CreateClient(httpClientName);
                using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                using var response =
                    await http.PostAsync(TvaEndpoint, content, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    throw new AnafVatCheckException(response.StatusCode, AnafHttp.Truncate(body),
                            $"ANAF request failed with status code {(int)response.StatusCode}.")
                        { RetryAfter = AnafHttp.GetRetryAfter(response) };

                AnafTvaResponse? dto;
                try
                {
                    dto = JsonSerializer.Deserialize(body, AnafVatCheckJsonContext.Default.AnafTvaResponse);
                }
                catch (JsonException exception)
                {
                    throw new AnafVatCheckException(response.StatusCode, AnafHttp.Truncate(body),
                        "ANAF returned a response body that could not be parsed.", exception);
                }

                if (dto is null)
                    throw new AnafVatCheckException(response.StatusCode, AnafHttp.Truncate(body),
                        "ANAF returned an empty response body.");
                // v9 signals success by returning found/notFound with no top-level code; a code is present only on
                // an error (or legacy) response, so treat any non-200 code (when present) as a fault.
                if (dto.Cod is { } cod && cod != 200)
                    throw new AnafVatCheckException(response.StatusCode, AnafHttp.Truncate(body),
                        $"ANAF returned error code {cod}: {dto.Message}.") { ErrorCode = cod };
                if (dto.Found is null && dto.NotFound is null)
                    throw new AnafVatCheckException(response.StatusCode, AnafHttp.Truncate(body),
                        "ANAF returned an unrecognized response (no found/notFound).");

                if (dto.Found is not null)
                    foreach (var item in dto.Found)
                        found.Add(Map(item));
                if (dto.NotFound is not null)
                    foreach (var item in dto.NotFound)
                        notFound.Add(item.Cui);
            }

            activity?.SetTag("anaf.found_count", found.Count);
            activity?.SetTag("anaf.not_found_count", notFound.Count);
            return new AnafBatchCompanyVatCheckResult { Found = found, NotFound = notFound };
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            throw;
        }
    }

    private AnafCompanyVatCheckResult Map(AnafFoundItem item)
    {
        var g = item.DateGenerale;
        return new AnafCompanyVatCheckResult
        {
            UniqueIdentificationCode = g?.Cui ?? 0,
            Name = g?.Denumire ?? string.Empty,
            Address = NullIfEmpty(g?.Adresa),
            PhoneNumber = NullIfEmpty(g?.Telefon),
            Fax = NullIfEmpty(g?.Fax),
            PostalCode = NullIfEmpty(g?.CodPostal),
            RegistrationNumber = NullIfEmpty(g?.NrRegCom),
            CaenCode = NullIfEmpty(g?.CodCaen),
            Iban = NullIfEmpty(g?.Iban),
            LegalForm = NullIfEmpty(g?.FormaJuridica),
            OrganizationForm = NullIfEmpty(g?.FormaOrganizare),
            OwnershipForm = NullIfEmpty(g?.FormaDeProprietate),
            FiscalAuthority = NullIfEmpty(g?.OrganFiscalCompetent),
            RegistrationStatus = NullIfEmpty(g?.StareInregistrare),
            RegistrationDate = ParseDate(g?.DataInregistrare),
            Act = NullIfEmpty(g?.Act),
            IsVatPayer = item.InregistrareScopTva?.ScpTva ?? false,
            IsInactive = item.StareInactiv?.StatusInactivi ?? false,
            UsesVatCashAccounting = item.InregistrareRtvai?.StatusTvaIncasare ?? false,
            UsesSplitVat = item.InregistrareSplitTva?.StatusSplitTva ?? false,
            UsesEInvoice = g?.StatusRoEFactura ?? false,
            RegisteredOfficeAddress = MapAddress(item.AdresaSediuSocial),
            FiscalDomicileAddress = MapAddress(item.AdresaDomiciliuFiscal),
            VatPeriods = item.InregistrareScopTva?.PerioadeTva is { } periods
                ? Array.ConvertAll(periods, MapVatPeriod)
                : []
        };
    }

    private AnafVatCheckAddress? MapAddress(IAnafVatCheckAddress? a)
    {
        if (a is null) return null;

        // Resolve the territorial unit from ANAF's (cod_Judet, cod_Localitate) via the embedded crosswalk, then
        // look the SIRUTA code up in the dataset. UAT = the unit itself when it is UAT-level, else its parent.
        var siruta = crosswalk.Find(a.CodJudet, a.CodLocalitate);
        var unit = siruta is { } code ? territorialUnits.Find(code) : null;
        var uat = unit switch
        {
            null => null,
            { Level: 2 } => unit,
            { Level: 3 } => territorialUnits.GetParent(unit),
            _ => null
        };
        // County via the plate code ("GJ"), falling back to the resolved unit's county.
        var county = (string.IsNullOrWhiteSpace(a.CodJudetAuto) ? null : territorialUnits.FindCounty(a.CodJudetAuto))
                     ?? (unit is null ? null : territorialUnits.GetCounty(unit));

        return new AnafVatCheckAddress
        {
            Street = NullIfEmpty(a.Strada),
            StreetNumber = NullIfEmpty(a.NumarStrada),
            Locality = NullIfEmpty(a.Localitate),
            LocalityCode = NullIfEmpty(a.CodLocalitate),
            County = NullIfEmpty(a.Judet),
            CountyCode = NullIfEmpty(a.CodJudet),
            CountyAutoCode = NullIfEmpty(a.CodJudetAuto),
            PostalCode = NullIfEmpty(a.CodPostal),
            Country = NullIfEmpty(a.Tara),
            Details = NullIfEmpty(a.Detalii),
            RomanianLocality = unit,
            RomanianUat = uat,
            RomanianCounty = county
        };
    }

    private static AnafVatCheckPeriod MapVatPeriod(AnafPerioadaTva p) => new()
    {
        StartDate = NullIfEmpty(p.DataInceput),
        EndDate = NullIfEmpty(p.DataSfarsit),
        Message = NullIfEmpty(p.Mesaj)
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ANAF dates come as "yyyy-MM-dd"; anything else (blank, malformed) yields null rather than throwing.
    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}