using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ES.FX.NousResearch.HermesAgent.Tests;

public class HermesAgentResponseGuardTests
{
    [Fact]
    public async Task Success_Response_Does_Not_Throw()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        await HermesAgentResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OpenAi_Envelope_Is_Parsed_Into_Error_Fields()
    {
        // The exact 401 body from the wire spec (no `param` member on this path).
        using var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                """{"error": {"message": "Invalid API key", "type": "invalid_request_error", "code": "invalid_api_key"}}""",
                Encoding.UTF8, "application/json")
        };

        var exception = await Assert.ThrowsAsync<HermesAgentApiException>(() =>
            HermesAgentResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal("Invalid API key", exception.Error?.Message);
        Assert.Equal("invalid_request_error", exception.Error?.Type);
        Assert.Equal("invalid_api_key", exception.Error?.Code);
        Assert.Null(exception.Error?.Param);
        Assert.Contains("Invalid API key", exception.ResponseBody);
        Assert.Contains("401", exception.Message);
    }

    [Fact]
    public async Task OpenAi_Envelope_With_Param_Is_Parsed()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"error": {"message": "Image parts must include a non-empty image URL.", "type": "invalid_request_error", "param": "messages[0].content", "code": "invalid_image_url"}}""",
                Encoding.UTF8, "application/json")
        };

        var exception = await Assert.ThrowsAsync<HermesAgentApiException>(() =>
            HermesAgentResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Equal("messages[0].content", exception.Error?.Param);
        Assert.Equal("invalid_image_url", exception.Error?.Code);
    }

    [Fact]
    public async Task Flat_Jobs_Envelope_Maps_To_Message_Only_Error()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error": "Invalid job ID format"}""", Encoding.UTF8,
                "application/json")
        };

        var exception = await Assert.ThrowsAsync<HermesAgentApiException>(() =>
            HermesAgentResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Equal("Invalid job ID format", exception.Error?.Message);
        Assert.Null(exception.Error?.Type);
        Assert.Null(exception.Error?.Code);
    }

    [Fact]
    public async Task Non_Json_Body_Yields_Null_Error_But_Preserves_Body()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>502 Bad Gateway</html>", Encoding.UTF8, "text/html")
        };

        var exception = await Assert.ThrowsAsync<HermesAgentApiException>(() =>
            HermesAgentResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.Null(exception.Error);
        Assert.Equal("<html>502 Bad Gateway</html>", exception.ResponseBody);
    }

    [Fact]
    public async Task Non_Object_Non_String_Error_Member_Yields_Null_Error()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"error": 42}""", Encoding.UTF8, "application/json")
        };

        var exception = await Assert.ThrowsAsync<HermesAgentApiException>(() =>
            HermesAgentResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Null(exception.Error);
        Assert.Contains("42", exception.ResponseBody);
    }

    [Fact]
    public async Task RetryAfter_Delta_Is_Surfaced()
    {
        // The concurrency cap sends `429` with `Retry-After: 1`.
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(
                """{"error": {"message": "Too many concurrent runs (max 10)", "type": "rate_limit_error", "param": null, "code": "rate_limit_exceeded"}}""",
                Encoding.UTF8, "application/json")
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));

        var exception = await Assert.ThrowsAsync<HermesAgentApiException>(() =>
            HermesAgentResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Equal(TimeSpan.FromSeconds(1), exception.RetryAfter);
        Assert.Equal("rate_limit_error", exception.Error?.Type);
        Assert.Equal("rate_limit_exceeded", exception.Error?.Code);
    }

    [Fact]
    public async Task RetryAfter_Date_Form_Is_Converted_To_A_Delay()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(120));

        var exception = await Assert.ThrowsAsync<HermesAgentApiException>(() =>
            HermesAgentResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.NotNull(exception.RetryAfter);
        Assert.InRange(exception.RetryAfter.Value, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120));
    }

    [Fact]
    public async Task RetryAfter_Date_In_The_Past_Clamps_To_Zero()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(-30));

        var exception = await Assert.ThrowsAsync<HermesAgentApiException>(() =>
            HermesAgentResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Equal(TimeSpan.Zero, exception.RetryAfter);
    }

    [Fact]
    public async Task Error_Body_Is_Capped_At_2048_Bytes()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(new string('x', 10_000))
        };

        var exception = await Assert.ThrowsAsync<HermesAgentApiException>(() =>
            HermesAgentResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Equal(2048, exception.ResponseBody!.Length);
        Assert.Null(exception.Error); // the truncated body is not valid JSON — parsed best-effort to null
    }

    [Fact]
    public async Task Empty_Body_Yields_Null_ResponseBody_And_Null_Error()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);

        var exception = await Assert.ThrowsAsync<HermesAgentApiException>(() =>
            HermesAgentResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Null(exception.ResponseBody);
        Assert.Null(exception.Error);
    }
}