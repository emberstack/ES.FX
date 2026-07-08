using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ES.FX.OpenData.Romania.Fiscal.Anaf.Internal;

internal sealed class AnafClient(
    IHttpClientFactory httpClientFactory,
    string httpClientName,
    AnafRequestThrottle throttle,
    AnafClientOptions options) : IAnafClient
{
    private const string TvaEndpoint = "PlatitorTvaRest/api/v9/ws/tva";

    public async Task<AnafCompany?> FindCompanyAsync(long cui, DateOnly? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var batch = await FindCompaniesAsync([cui], asOf, cancellationToken).ConfigureAwait(false);
        return batch.Found.Count > 0 ? batch.Found[0] : null;
    }

    public async Task<AnafCompanyBatch> FindCompaniesAsync(IReadOnlyCollection<long> cuis, DateOnly? asOf = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cuis);

        var found = new List<AnafCompany>();
        var notFound = new List<long>();
        if (cuis.Count == 0) return new AnafCompanyBatch { Found = found, NotFound = notFound };

        using var activity = AnafClientInstrumentation.ActivitySource.StartActivity(
            "ANAF FindCompanies", ActivityKind.Client);
        activity?.SetTag("anaf.cui_count", cuis.Count);

        var date = (asOf ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        foreach (var chunk in cuis.Chunk(options.EffectiveBatchSize))
        {
            var requestItems = Array.ConvertAll(chunk, c => new AnafTvaRequestItem { Cui = c, Data = date });
            var requestJson = JsonSerializer.Serialize(requestItems, AnafJsonContext.Default.AnafTvaRequestItemArray);

            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);

            var http = httpClientFactory.CreateClient(httpClientName);
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(TvaEndpoint, content, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new AnafApiException(response.StatusCode, OpenDataHttp.Truncate(body),
                        $"ANAF request failed with status code {(int)response.StatusCode}.")
                    { RetryAfter = OpenDataHttp.GetRetryAfter(response) };

            AnafTvaResponse? dto;
            try
            {
                dto = JsonSerializer.Deserialize(body, AnafJsonContext.Default.AnafTvaResponse);
            }
            catch (JsonException exception)
            {
                throw new AnafApiException(response.StatusCode, OpenDataHttp.Truncate(body),
                    "ANAF returned a response body that could not be parsed.", exception);
            }

            if (dto is null)
                throw new AnafApiException(response.StatusCode, OpenDataHttp.Truncate(body),
                    "ANAF returned an empty response body.");
            if (dto.Cod != 200)
                throw new AnafApiException(response.StatusCode, OpenDataHttp.Truncate(body),
                    $"ANAF returned error code {dto.Cod}: {dto.Message}.") { ErrorCode = dto.Cod };

            if (dto.Found is not null)
                foreach (var item in dto.Found)
                    found.Add(Map(item));
            if (dto.NotFound is not null)
                foreach (var item in dto.NotFound)
                    notFound.Add(item.Cui);
        }

        return new AnafCompanyBatch { Found = found, NotFound = notFound };
    }

    private static AnafCompany Map(AnafFoundItem item)
    {
        var general = item.DateGenerale;
        return new AnafCompany
        {
            Cui = general?.Cui ?? 0,
            Name = general?.Denumire ?? string.Empty,
            RegistrationNumber = NullIfEmpty(general?.NrRegCom),
            Address = NullIfEmpty(general?.Adresa),
            PhoneNumber = NullIfEmpty(general?.Telefon),
            IsVatPayer = item.InregistrareScopTva?.ScpTva ?? false,
            IsInactive = item.StareInactiv?.StatusInactivi ?? false
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
