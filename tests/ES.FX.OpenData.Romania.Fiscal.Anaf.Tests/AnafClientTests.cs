using System.Diagnostics;
using System.Net;
using System.Text;
using ES.FX.OpenData;
using ES.FX.OpenData.Romania.Fiscal.Anaf;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData.Romania.Fiscal.Anaf.Tests;

public class AnafClientTests
{
    private static readonly DateOnly AsOf = new(2025, 12, 1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (IAnafClient Client, StubHttpMessageHandler Handler) Build(
        Func<int, HttpResponseMessage> responder, Action<AnafClientOptions>? configure = null)
    {
        var handler = new StubHttpMessageHandler(responder);
        var services = new ServiceCollection();
        services.AddOpenData().AddRomaniaAnaf(
            o =>
            {
                o.RequestsPerSecond = 0; // disable throttle so tests are fast
                configure?.Invoke(o);
            },
            b => b.ConfigurePrimaryHttpMessageHandler(() => handler));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IAnafClient>(), handler);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Found_company_is_mapped_from_v9_shape()
    {
        var (client, _) = Build(_ => Json(
            """{"cod":200,"message":"SUCCESS","found":[{"date_generale":{"cui":123,"denumire":"ACME SRL","adresa":"Str. Exemplu 1","nrRegCom":"J40/1/2000","telefon":"021"},"inregistrare_scop_Tva":{"scpTVA":true},"stare_inactiv":{"statusInactivi":false}}],"notFound":[]}"""));

        var company = await client.FindCompanyAsync(123, AsOf, Ct);

        Assert.NotNull(company);
        Assert.Equal(123, company!.Cui);
        Assert.Equal("ACME SRL", company.Name);
        Assert.Equal("J40/1/2000", company.RegistrationNumber);
        Assert.Equal("Str. Exemplu 1", company.Address);
        Assert.True(company.IsVatPayer);
        Assert.False(company.IsInactive);
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
        Assert.Equal(1, batch.Found[0].Cui);
        Assert.Equal([2L], batch.NotFound);
    }

    [Fact]
    public async Task Large_batches_are_chunked_and_merged()
    {
        var (client, handler) = Build(
            index => index == 0
                ? Json("""{"cod":200,"found":[{"date_generale":{"cui":1,"denumire":"A"}},{"date_generale":{"cui":2,"denumire":"B"}}],"notFound":[]}""")
                : Json("""{"cod":200,"found":[{"date_generale":{"cui":3,"denumire":"C"}}],"notFound":[]}"""),
            o => o.BatchSize = 2);

        var batch = await client.FindCompaniesAsync([1, 2, 3], AsOf, Ct);

        Assert.Equal(2, handler.CallCount); // 3 CUIs / batch size 2 => 2 requests
        Assert.Equal(3, batch.Found.Count);
        Assert.Equal([1L, 2L, 3L], batch.Found.Select(c => c.Cui));
    }

    [Fact]
    public async Task Non_200_anaf_code_throws()
    {
        var (client, _) = Build(_ => Json("""{"cod":500,"message":"boom"}"""));
        await Assert.ThrowsAsync<AnafApiException>(() => client.FindCompanyAsync(1, AsOf, Ct));
    }

    [Fact]
    public async Task Http_error_throws_with_status_code()
    {
        var (client, _) = Build(_ => Json("gateway down", HttpStatusCode.BadGateway));
        var exception = await Assert.ThrowsAsync<AnafApiException>(() => client.FindCompanyAsync(1, AsOf, Ct));
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
        var exception = await Assert.ThrowsAsync<AnafApiException>(() => client.FindCompanyAsync(1, AsOf, Ct));
        Assert.Equal(401, exception.ErrorCode);
    }

    [Fact]
    public async Task Emits_a_client_activity_on_the_named_source()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "ES.FX.OpenData.Romania.Fiscal.Anaf",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        var (client, _) = Build(_ => Json("""{"cod":200,"found":[],"notFound":[{"cui":1}]}"""));
        await client.FindCompanyAsync(1, AsOf, Ct);

        Assert.Contains(activities, a => a.DisplayName == "ANAF FindCompanies");
    }
}
