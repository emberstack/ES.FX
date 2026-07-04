using ES.FX.Additions.KubernetesClient.Models;
using k8s.Models;

namespace ES.FX.Additions.KubernetesClient.Tests;

public class NamespacedNameTests
{
    // ---- TryParse: valid inputs -------------------------------------------------

    [Fact]
    public void TryParse_NamespaceSlashName_ParsesBothParts()
    {
        Assert.True(NamespacedName.TryParse("kube-system/coredns", out var nsName));
        Assert.Equal("kube-system", nsName.Namespace);
        Assert.Equal("coredns", nsName.Name);
    }

    [Fact]
    public void TryParse_BareName_IsClusterScoped()
    {
        Assert.True(NamespacedName.TryParse("my-node", out var nsName));
        Assert.Equal(string.Empty, nsName.Namespace);
        Assert.Equal("my-node", nsName.Name);
    }

    [Fact]
    public void TryParse_TrimsWhitespaceAroundParts()
    {
        Assert.True(NamespacedName.TryParse("  ns  /  name  ", out var nsName));
        Assert.Equal("ns", nsName.Namespace);
        Assert.Equal("name", nsName.Name);
    }

    // ---- TryParse: malformed inputs must be REJECTED ----------------------------

    [Theory]
    [InlineData("ns/")] // trailing slash, empty name
    [InlineData("/name")] // leading slash, empty namespace
    [InlineData("a//b")] // double slash => 3 segments
    [InlineData("/")] // both empty
    [InlineData("//")] // 3 empty segments
    [InlineData("a/b/c")] // too many segments
    [InlineData("  /  ")] // whitespace-only around slash
    [InlineData("ns/ ")] // whitespace-only name
    [InlineData(" /name")] // whitespace-only namespace
    [InlineData("")] // empty
    [InlineData("   ")] // whitespace
    [InlineData(null)] // null
    public void TryParse_MalformedInput_ReturnsFalseAndEmpty(string? value)
    {
        Assert.False(NamespacedName.TryParse(value, out var nsName));
        Assert.Same(NamespacedName.Empty, nsName);
    }

    // ---- string constructor mirrors TryParse ------------------------------------

    [Fact]
    public void StringConstructor_ValidNamespaceSlashName_Constructs()
    {
        var nsName = new NamespacedName("team-a/service");
        Assert.Equal("team-a", nsName.Namespace);
        Assert.Equal("service", nsName.Name);
    }

    [Fact]
    public void StringConstructor_BareName_IsClusterScoped()
    {
        var nsName = new NamespacedName("cluster-role");
        Assert.Equal(string.Empty, nsName.Namespace);
        Assert.Equal("cluster-role", nsName.Name);
    }

    [Theory]
    [InlineData("ns/")]
    [InlineData("/name")]
    [InlineData("a//b")]
    [InlineData("/")]
    [InlineData("a/b/c")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void StringConstructor_MalformedInput_Throws(string? value)
    {
        var ex = Assert.Throws<ArgumentException>(() => { _ = new NamespacedName(value); });
        Assert.Equal("value", ex.ParamName);
    }

    // ---- two-arg constructor ----------------------------------------------------

    [Fact]
    public void TwoArgConstructor_TrimsAndKeepsValues()
    {
        var nsName = new NamespacedName("  ns  ", "  name  ");
        Assert.Equal("ns", nsName.Namespace);
        Assert.Equal("name", nsName.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TwoArgConstructor_NullOrWhitespace_BecomesEmpty(string? blank)
    {
        var nsName = new NamespacedName(blank, blank);
        Assert.Equal(string.Empty, nsName.Namespace);
        Assert.Equal(string.Empty, nsName.Name);
    }

    // ---- V1ObjectMeta constructor -----------------------------------------------

    [Fact]
    public void MetadataConstructor_UsesNamespaceAndName()
    {
        var meta = new V1ObjectMeta { Name = "pod-1", NamespaceProperty = "default" };
        var nsName = new NamespacedName(meta);
        Assert.Equal("default", nsName.Namespace);
        Assert.Equal("pod-1", nsName.Name);
    }

    // ---- Empty ------------------------------------------------------------------

    [Fact]
    public void Empty_HasEmptyNamespaceAndName()
    {
        Assert.Equal(string.Empty, NamespacedName.Empty.Namespace);
        Assert.Equal(string.Empty, NamespacedName.Empty.Name);
    }

    // ---- Equality ---------------------------------------------------------------

    [Fact]
    public void Equals_SameNamespaceAndName_AreEqual()
    {
        var a = new NamespacedName("ns", "name");
        var b = new NamespacedName("ns", "name");
        Assert.Equal(a, b);
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_ParsedAndConstructed_AreEqual()
    {
        var parsed = new NamespacedName("ns/name");
        var built = new NamespacedName("ns", "name");
        Assert.Equal(built, parsed);
    }

    [Fact]
    public void Equals_DifferentNamespace_AreNotEqual()
    {
        Assert.NotEqual(new NamespacedName("ns1", "name"), new NamespacedName("ns2", "name"));
    }

    [Fact]
    public void Equals_DifferentName_AreNotEqual()
    {
        Assert.NotEqual(new NamespacedName("ns", "name1"), new NamespacedName("ns", "name2"));
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        Assert.False(new NamespacedName("ns", "name").Equals(null));
    }

    [Fact]
    public void Equals_SameReference_ReturnsTrue()
    {
        var a = new NamespacedName("ns", "name");
        Assert.True(a.Equals(a));
    }

    // ---- ToString ---------------------------------------------------------------

    [Fact]
    public void ToString_Namespaced_UsesSlashFormat()
    {
        Assert.Equal("ns/name", new NamespacedName("ns", "name").ToString());
    }

    [Fact]
    public void ToString_ClusterScoped_OmitsNamespaceAndSlash()
    {
        Assert.Equal("name", new NamespacedName(string.Empty, "name").ToString());
    }

    [Fact]
    public void ToString_RoundTripsThroughStringConstructor()
    {
        var original = new NamespacedName("ns", "name");
        var roundTripped = new NamespacedName(original.ToString());
        Assert.Equal(original, roundTripped);
    }
}