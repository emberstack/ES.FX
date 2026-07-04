using System.Globalization;
using ES.FX.Additions.KubernetesClient.Models.Extensions;
using k8s.Models;

namespace ES.FX.Additions.KubernetesClient.Tests;

public class V1ObjectMetaExtensionsTests
{
    private static V1ObjectMeta MetaWithAnnotations(params (string Key, string Value)[] annotations)
    {
        var meta = new V1ObjectMeta
        {
            Annotations = annotations.ToDictionary(a => a.Key, a => a.Value)
        };
        return meta;
    }

    // ---- NamespacedName() on IKubernetesObject ----------------------------------

    [Fact]
    public void NamespacedName_FromObject_UsesMetadata()
    {
        var pod = new V1Pod
        {
            Metadata = new V1ObjectMeta { Name = "web", NamespaceProperty = "prod" }
        };

        var nsName = pod.NamespacedName();

        Assert.Equal("prod", nsName.Namespace);
        Assert.Equal("web", nsName.Name);
    }

    [Fact]
    public void NamespacedName_FromNullObject_ReturnsEmptyLike()
    {
        IKubernetesObjectHelper.Null(out var pod);

        var nsName = pod.NamespacedName();

        Assert.Equal(string.Empty, nsName.Namespace);
        Assert.Equal(string.Empty, nsName.Name);
    }

    // ---- TryGetAnnotationValue --------------------------------------------------

    [Fact]
    public void TryGetAnnotationValue_String_ReturnsRawValue()
    {
        var meta = MetaWithAnnotations(("owner", "team-blue"));

        Assert.True(meta.TryGetAnnotationValue<string>("owner", out var value));
        Assert.Equal("team-blue", value);
    }

    [Fact]
    public void TryGetAnnotationValue_Int_ParsesValue()
    {
        var meta = MetaWithAnnotations(("replicas", "3"));

        Assert.True(meta.TryGetAnnotationValue<int>("replicas", out var value));
        Assert.Equal(3, value);
    }

    [Fact]
    public void TryGetAnnotationValue_Bool_ParsesValue()
    {
        var meta = MetaWithAnnotations(("enabled", "true"));

        Assert.True(meta.TryGetAnnotationValue<bool>("enabled", out var value));
        Assert.True(value);
    }

    [Fact]
    public void TryGetAnnotationValue_TrimsRawValueBeforeParsing()
    {
        var meta = MetaWithAnnotations(("replicas", "  42  "));

        Assert.True(meta.TryGetAnnotationValue<int>("replicas", out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetAnnotationValue_Double_UsesInvariantCulture()
    {
        // A German/French thread would parse "1,5" as 1.5 with comma; invariant must use the dot.
        var meta = MetaWithAnnotations(("ratio", "1.5"));

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.True(meta.TryGetAnnotationValue<double>("ratio", out var value));
            Assert.Equal(1.5d, value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void TryGetAnnotationValue_InvariantParse_RejectsCultureSpecificFormat()
    {
        // "1,5" is not a valid invariant double (comma is a thousands separator, not decimal),
        // and TypeConverter for double rejects it -> false, even under a comma-decimal culture.
        var meta = MetaWithAnnotations(("ratio", "1,5"));

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.False(meta.TryGetAnnotationValue<double>("ratio", out var value));
            Assert.Equal(default, value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void TryGetAnnotationValue_MissingKey_ReturnsFalse()
    {
        var meta = MetaWithAnnotations(("present", "value"));

        Assert.False(meta.TryGetAnnotationValue<string>("absent", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryGetAnnotationValue_NullAnnotations_ReturnsFalse()
    {
        var meta = new V1ObjectMeta(); // Annotations is null

        Assert.False(meta.TryGetAnnotationValue<string>("anything", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryGetAnnotationValue_Unparseable_ReturnsFalse()
    {
        var meta = MetaWithAnnotations(("replicas", "not-a-number"));

        Assert.False(meta.TryGetAnnotationValue<int>("replicas", out var value));
        Assert.Equal(default, value);
    }

    [Fact]
    public void TryGetAnnotationValue_NullMetadata_Throws()
    {
        V1ObjectMeta meta = null!;
        Assert.Throws<ArgumentNullException>(() => meta.TryGetAnnotationValue<string>("key", out _));
    }

    [Fact]
    public void TryGetAnnotationValue_NullKey_Throws()
    {
        var meta = MetaWithAnnotations(("key", "value"));
        Assert.Throws<ArgumentNullException>(() => meta.TryGetAnnotationValue<string>(null!, out _));
    }
}

/// <summary>Helper to produce a strongly-typed null of a concrete Kubernetes object type.</summary>
internal static class IKubernetesObjectHelper
{
    public static void Null(out V1Pod? pod) => pod = null;
}