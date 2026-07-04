using System.Diagnostics.CodeAnalysis;
using ES.FX.TransactionalOutbox.Serialization;

namespace ES.FX.TransactionalOutbox.Tests;

public class DefaultPayloadTypeProviderTests
{
    private static readonly IReadOnlyDictionary<string, string> NoHeaders =
        new Dictionary<string, string>();

    [Fact]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test resolves types via reflection.")]
    public void GetType_Resolves_Type_From_AssemblyQualifiedName()
    {
        var provider = new DefaultPayloadTypeProvider();
        var aqn = typeof(Probe).AssemblyQualifiedName!;

        var resolved = provider.GetType(aqn, NoHeaders);

        Assert.Equal(typeof(Probe), resolved);
    }

    [Fact]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test resolves types via reflection.")]
    public void GetType_Resolves_BuiltIn_Type()
    {
        var provider = new DefaultPayloadTypeProvider();

        var resolved = provider.GetType(typeof(string).AssemblyQualifiedName!, NoHeaders);

        Assert.Equal(typeof(string), resolved);
    }

    [Fact]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test resolves types via reflection.")]
    public void GetType_Throws_InvalidOperationException_For_Unknown_Type()
    {
        var provider = new DefaultPayloadTypeProvider();

        // Documents CURRENT behavior: the default provider throws (does NOT return null)
        // when the type cannot be resolved from its name.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            provider.GetType("This.Type.Does.Not.Exist, Nonexistent.Assembly", NoHeaders));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetPayloadType_Default_Returns_AssemblyQualifiedName()
    {
        IPayloadTypeProvider provider = new DefaultPayloadTypeProvider();

        var payloadType = provider.GetPayloadType(typeof(Probe));

        Assert.Equal(typeof(Probe).AssemblyQualifiedName, payloadType);
    }

    [Fact]
    public void SetTypeHeaders_Default_Is_NoOp()
    {
        IPayloadTypeProvider provider = new DefaultPayloadTypeProvider();
        var headers = new Dictionary<string, string> { ["existing"] = "value" };

        provider.SetTypeHeaders(typeof(Probe), headers);

        // Default implementation must not mutate the headers.
        Assert.Single(headers);
        Assert.Equal("value", headers["existing"]);
    }

    [Fact]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test resolves types via reflection.")]
    public void GetPayloadType_Then_GetType_RoundTrips()
    {
        IPayloadTypeProvider provider = new DefaultPayloadTypeProvider();

        var name = provider.GetPayloadType(typeof(Probe));
        var resolved = provider.GetType(name, NoHeaders);

        Assert.Equal(typeof(Probe), resolved);
    }

    public sealed record Probe(int Value);
}