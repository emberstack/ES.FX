using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ES.FX.OpenData.Romania.Anaf.VatCheck.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData.Romania.Anaf.VatCheck.Tests;

public class AnafVatCheckClientTests
{
    private static readonly DateOnly AsOf = new(2025, 12, 1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (IAnafVatCheckClient Client, StubHttpMessageHandler Handler) Build(
        Func<int, HttpResponseMessage> responder, Action<AnafVatCheckClientOptions>? configure = null)
    {
        var handler = new StubHttpMessageHandler(responder);
        var services = new ServiceCollection();
        services.AddRomaniaAnaf(
            o =>
            {
                o.RequestsPerSecond = 0; // disable throttle so tests are fast
                configure?.Invoke(o);
            },
            b => b.ConfigurePrimaryHttpMessageHandler(() => handler));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IAnafVatCheckClient>(), handler);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Found_company_is_mapped_from_the_v9_shape_including_structured_addresses()
    {
        // Representative v9 payload (fictional company): NO top-level "cod"; found[] with date_generale + the two structured address blocks.
        var (client, handler) = Build(_ => Json(
            """
            {"found":[{
              "date_generale":{"cui":12345678,"denumire":"CONTOSO SRL","adresa":"JUD. CLUJ, MUN. CLUJ-NAPOCA","telefon":"0000000000","codPostal":"400335","nrRegCom":"J12/1/2013","cod_CAEN":"6202","data_inregistrare":"2013-12-20","statusRO_e_Factura":true},
              "inregistrare_scop_Tva":{"scpTVA":true,"perioade_TVA":[{"data_inceput_ScpTVA":"2014-01-09","data_sfarsit_ScpTVA":"","mesaj_ScpTVA":""}]},
              "inregistrare_RTVAI":{"statusTvaIncasare":true},
              "stare_inactiv":{"statusInactivi":false},
              "inregistrare_SplitTVA":{"statusSplitTVA":false},
              "adresa_sediu_social":{"sdenumire_Localitate":"Mun. Cluj-Napoca","sdenumire_Strada":"Str. Exemplu","scod_Localitate":"103","sdenumire_Judet":"CLUJ","scod_Judet":"12","scod_JudetAuto":"CJ","scod_Postal":"400335"},
              "adresa_domiciliu_fiscal":{"ddenumire_Localitate":"Mun. Cluj-Napoca","dcod_Localitate":"103","ddenumire_Judet":"CLUJ","dcod_Judet":"12","dcod_JudetAuto":"CJ","dcod_Postal":"400335"}
            }],"notFound":[]}
            """));

        var company = await client.FindCompanyAsync(12345678, AsOf, Ct);

        Assert.NotNull(company);
        Assert.Equal(12345678, company!.UniqueIdentificationCode);
        Assert.Equal("CONTOSO SRL", company.Name);
        Assert.Equal("JUD. CLUJ, MUN. CLUJ-NAPOCA", company.Address);
        Assert.Equal("J12/1/2013", company.RegistrationNumber);
        Assert.Equal("6202", company.CaenCode);
        Assert.True(company.IsVatPayer);
        Assert.True(company.UsesVatCashAccounting);
        Assert.False(company.UsesSplitVat);
        Assert.True(company.UsesEInvoice);
        Assert.False(company.IsInactive);

        // Structured addresses — raw codes preserved; everything territorial is resolved to SIRUTA.
        var office = company.RegisteredOfficeAddress!;
        Assert.Equal("CJ", office.CountyAutoCode);
        Assert.Equal("12", office.CountyCode);
        Assert.Equal("103", office.LocalityCode);
        Assert.Equal("Mun. Cluj-Napoca", office.Locality);
        Assert.Equal("400335", office.PostalCode);
        Assert.Equal("RO-CJ", office.RomanianCounty!.IsoCode); // county via CountyAutoCode "CJ"
        // Locality + UAT resolve through the embedded ANAF→SIRUTA crosswalk (12/103 → SIRUTA 54984).
        Assert.Equal(54984, office.RomanianLocality!.SirutaCode);
        Assert.Equal(3, office.RomanianLocality.Level); // NIV 3 locality
        Assert.Equal(54975, office.RomanianUat!.SirutaCode); // parent UAT: Municipiul Cluj-Napoca
        Assert.Equal(2, office.RomanianUat.Level); // NIV 2 UAT

        // The fiscal-domicile address points at the same territory and is resolved just the same.
        var domicile = company.FiscalDomicileAddress!;
        Assert.Equal("CJ", domicile.CountyAutoCode);
        Assert.Equal(54984, domicile.RomanianLocality!.SirutaCode);
        Assert.Equal(54975, domicile.RomanianUat!.SirutaCode);
        Assert.Equal("RO-CJ", domicile.RomanianCounty!.IsoCode);

        // VAT-registration history.
        Assert.Single(company.VatPeriods);
        Assert.Equal("2014-01-09", company.VatPeriods[0].StartDate);

        // Regression guard for the endpoint fix (v9 uses /api/PlatitorTvaRest/v9/tva, not /PlatitorTvaRest/api/v9/ws/tva).
        Assert.EndsWith("api/PlatitorTvaRest/v9/tva", handler.LastRequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Not_found_cui_returns_null()
    {
        var (client, _) = Build(_ => Json("""{"cod":200,"message":"SUCCESS","found":[],"notFound":[{"cui":999}]}"""));
        Assert.Null(await client.FindCompanyAsync(999, AsOf, Ct));
    }

    [Fact]
    public async Task Batch_splits_found_and_not_found()
    {
        var (client, _) = Build(_ => Json(
            """{"cod":200,"found":[{"date_generale":{"cui":1,"denumire":"A"}}],"notFound":[{"cui":2}]}"""));

        var batch = await client.FindCompaniesAsync([1, 2], AsOf, Ct);

        Assert.Single(batch.Found);
        Assert.Equal(1, batch.Found[0].UniqueIdentificationCode);
        Assert.Equal([2L], batch.NotFound);
    }

    [Fact]
    public async Task Large_batches_are_chunked_and_merged()
    {
        var (client, handler) = Build(
            index => index == 0
                ? Json(
                    """{"cod":200,"found":[{"date_generale":{"cui":1,"denumire":"A"}},{"date_generale":{"cui":2,"denumire":"B"}}],"notFound":[]}""")
                : Json("""{"cod":200,"found":[{"date_generale":{"cui":3,"denumire":"C"}}],"notFound":[]}"""),
            o => o.BatchSize = 2);

        var batch = await client.FindCompaniesAsync([1, 2, 3], AsOf, Ct);

        Assert.Equal(2, handler.CallCount); // 3 CUIs / batch size 2 => 2 requests
        Assert.Equal(3, batch.Found.Count);
        Assert.Equal([1L, 2L, 3L], batch.Found.Select(c => c.UniqueIdentificationCode));
    }

    [Fact]
    public async Task Non_200_anaf_code_throws()
    {
        var (client, _) = Build(_ => Json("""{"cod":500,"message":"boom"}"""));
        await Assert.ThrowsAsync<AnafVatCheckException>(() => client.FindCompanyAsync(1, AsOf, Ct));
    }

    [Fact]
    public async Task Http_error_throws_with_status_code()
    {
        var (client, _) = Build(_ => Json("gateway down", HttpStatusCode.BadGateway));
        var exception = await Assert.ThrowsAsync<AnafVatCheckException>(() => client.FindCompanyAsync(1, AsOf, Ct));
        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
    }

    [Fact]
    public async Task Empty_input_makes_no_request()
    {
        var (client, handler) = Build(_ => Json("""{"cod":200,"found":[],"notFound":[]}"""));
        var batch = await client.FindCompaniesAsync([], AsOf, Ct);
        Assert.Empty(batch.Found);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Business_error_code_is_exposed_structurally()
    {
        var (client, _) = Build(_ => Json("""{"cod":401,"message":"unauthorized"}"""));
        var exception = await Assert.ThrowsAsync<AnafVatCheckException>(() => client.FindCompanyAsync(1, AsOf, Ct));
        Assert.Equal(401, exception.ErrorCode);
    }

    [Fact]
    public async Task Emits_a_client_activity_on_the_named_source()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "ES.FX.OpenData.Romania.Anaf.VatCheck",
            Sample = (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        var (client, _) = Build(_ => Json("""{"cod":200,"found":[],"notFound":[{"cui":1}]}"""));
        await client.FindCompanyAsync(1, AsOf, Ct);

        Assert.Contains(activities, a => a.DisplayName == "ANAF FindCompanies");
    }


    // ----- Parity + documented limits (added in the ANAF review pass) -----

    [Fact]
    public void Defaults_match_anaf_documented_limits()
    {
        // ANAF doc_WS_V9: "Un request poate contine maxim 100 de CUI-uri. Un client poate executa maxim 1 request pe secunda."
        var options = new AnafVatCheckClientOptions();
        Assert.Equal(1, options.RequestsPerSecond);
        Assert.Equal(100, options.BatchSize);
        Assert.Equal(TimeSpan.FromSeconds(1), options.RequestInterval);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)] // ANAF's documented hard limit
    [InlineData(0, false)]
    [InlineData(101, false)] // over the limit: reject at startup instead of silently dropping CUIs past 100
    [InlineData(500, false)]
    public void BatchSize_must_be_between_1_and_100(int batchSize, bool expectedValid)
    {
        var validator = new AnafVatCheckClientOptionsValidator();
        var result = validator.Validate(null, new AnafVatCheckClientOptions { BatchSize = batchSize });
        Assert.Equal(expectedValid, result.Succeeded);
    }

    [Fact]
    public async Task Malformed_json_body_throws_typed_parse_exception()
    {
        var (client, _) = Build(_ => Json("this is not json {"));
        var exception = await Assert.ThrowsAsync<AnafVatCheckException>(() => client.FindCompanyAsync(1, AsOf, Ct));
        Assert.Contains("could not be parsed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Empty_body_throws_typed_exception()
    {
        var (client, _) = Build(_ => Json("null")); // JSON null deserializes to a null DTO
        var exception = await Assert.ThrowsAsync<AnafVatCheckException>(() => client.FindCompanyAsync(1, AsOf, Ct));
        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Retry_after_header_is_surfaced_on_the_exception()
    {
        var (client, _) = Build(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("busy", Encoding.UTF8, "text/plain")
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return response;
        });

        var exception = await Assert.ThrowsAsync<AnafVatCheckException>(() => client.FindCompanyAsync(1, AsOf, Ct));
        Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
    }

    [Fact]
    public async Task Null_cuis_throws_argument_null_exception()
    {
        var (client, _) = Build(_ => Json("""{"cod":200,"found":[],"notFound":[]}"""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.FindCompaniesAsync(null!, AsOf, Ct));
    }

    [Fact]
    public async Task Unknown_locality_code_leaves_the_unit_unresolved_but_still_resolves_the_county()
    {
        // A (cod_Judet, cod_Localitate) pair that is not in the crosswalk: the locality/UAT can't be resolved,
        // but the county still comes from the plate code so the address isn't left entirely unlinked.
        var (client, _) = Build(_ => Json(
            """
            {"found":[{
              "date_generale":{"cui":1,"denumire":"X"},
              "adresa_sediu_social":{"scod_Localitate":"999999","scod_Judet":"12","scod_JudetAuto":"CJ"}
            }],"notFound":[]}
            """));

        var company = await client.FindCompanyAsync(1, AsOf, Ct);

        var office = company!.RegisteredOfficeAddress!;
        Assert.Null(office.RomanianLocality);
        Assert.Null(office.RomanianUat);
        Assert.Equal("RO-CJ", office.RomanianCounty!.IsoCode);
    }

    [Fact]
    public async Task Address_without_location_codes_resolves_nothing()
    {
        var (client, _) = Build(_ => Json(
            """{"found":[{"date_generale":{"cui":1,"denumire":"X"},"adresa_sediu_social":{"sdenumire_Strada":"Str. Test"}}],"notFound":[]}"""));

        var office = (await client.FindCompanyAsync(1, AsOf, Ct))!.RegisteredOfficeAddress!;
        Assert.Null(office.RomanianLocality);
        Assert.Null(office.RomanianUat);
        Assert.Null(office.RomanianCounty);
    }

    [Fact]
    public void Crosswalk_resolves_anaf_codes_from_the_embedded_resource()
    {
        var crosswalk = new AnafSirutaCrosswalk();

        Assert.Equal(54984, crosswalk.Find("12", "103")); // Cluj-Napoca
        Assert.Null(crosswalk.Find("12", "999999")); // unknown locality
        Assert.Null(crosswalk.Find(null, "103")); // blank county
        Assert.Null(crosswalk.Find("12", " ")); // blank locality
    }

    [Fact]
    public void AddRomaniaAnaf_is_idempotent()
    {
        var services = new ServiceCollection();
        services.AddRomaniaAnaf();
        services.AddRomaniaAnaf();

        var provider = services.BuildServiceProvider();
        var httpClient = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient("ES.FX.OpenData.Romania.Anaf.VatCheck");

        // The guard must stop a second registration from stacking the client config or the service.
        Assert.Single(httpClient.DefaultRequestHeaders.Accept);
        Assert.Single(provider.GetServices<IAnafVatCheckClient>());
    }
}