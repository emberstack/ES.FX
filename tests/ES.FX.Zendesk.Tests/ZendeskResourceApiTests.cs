using ES.FX.Zendesk.Tests.Testing;
using ES.FX.Zendesk.Users;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests;

/// <summary>
///     Behavioral coverage of the shared <see cref="ZendeskResourceApi" /> request flow (exercised through the
///     Users area): the literal-<c>null</c> deserialize branch and cancellation surfacing.
/// </summary>
public class ZendeskResourceApiTests
{
    private static ZendeskUsersApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskUsersApi>.Instance);

    [Fact]
    public async Task Literal_Null_Body_Throws_InvalidOperationException_Naming_The_Operation()
    {
        // A 200 whose body is the JSON literal `null` deserializes to a null payload — distinct from the
        // `{}` empty-envelope branch (a non-null wrapper with a null property, covered elsewhere). The
        // rejection must name the operation for diagnosability.
        var users = CreateApi(new StubHttpMessageHandler("null"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await users.GetCurrentUserAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Zendesk.Users.GetCurrent", exception.Message);
    }

    [Fact]
    public async Task PreCancelled_Token_Surfaces_OperationCanceledException()
    {
        var users = CreateApi(new StubHttpMessageHandler("""{ "user": { "id": 42 } }"""));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await users.GetCurrentUserAsync(cts.Token));
    }
}
