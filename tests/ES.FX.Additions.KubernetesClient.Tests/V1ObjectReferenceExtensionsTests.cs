using ES.FX.Additions.KubernetesClient.Models.Extensions;
using k8s;
using k8s.Models;

namespace ES.FX.Additions.KubernetesClient.Tests;

public class V1ObjectReferenceExtensionsTests
{
    private static V1Pod BuildPod() => new()
    {
        ApiVersion = "v1",
        Kind = "Pod",
        Metadata = new V1ObjectMeta
        {
            Name = "web",
            NamespaceProperty = "prod",
            Uid = "uid-123",
            ResourceVersion = "42"
        }
    };

    // ---- ObjectReference from IKubernetesObject ---------------------------------

    [Fact]
    public void ObjectReference_FromObject_CopiesAllReferenceFields()
    {
        var reference = BuildPod().ObjectReference();

        Assert.Equal("v1", reference.ApiVersion);
        Assert.Equal("Pod", reference.Kind);
        Assert.Equal("web", reference.Name);
        Assert.Equal("prod", reference.NamespaceProperty);
        Assert.Equal("42", reference.ResourceVersion);
        Assert.Equal("uid-123", reference.Uid);
    }

    [Fact]
    public void ObjectReference_FromNullObject_Throws()
    {
        IKubernetesObject<V1ObjectMeta> obj = null!;
        Assert.Throws<ArgumentNullException>(() => { _ = obj.ObjectReference(); });
    }

    // ---- ObjectReference from V1ObjectMeta --------------------------------------

    [Fact]
    public void ObjectReference_FromMetadata_CopiesMetadataFields()
    {
        var meta = new V1ObjectMeta
        {
            Name = "web",
            NamespaceProperty = "prod",
            Uid = "uid-123",
            ResourceVersion = "42"
        };

        var reference = meta.ObjectReference();

        Assert.Equal("web", reference.Name);
        Assert.Equal("prod", reference.NamespaceProperty);
        Assert.Equal("uid-123", reference.Uid);
        Assert.Equal("42", reference.ResourceVersion);
    }

    [Fact]
    public void ObjectReference_FromMetadata_DoesNotPopulateApiVersionOrKind()
    {
        // Per the library's documented remark: metadata source cannot know ApiVersion/Kind.
        var reference = new V1ObjectMeta { Name = "web" }.ObjectReference();

        Assert.Null(reference.ApiVersion);
        Assert.Null(reference.Kind);
    }

    [Fact]
    public void ObjectReference_FromNullMetadata_Throws()
    {
        V1ObjectMeta meta = null!;
        Assert.Throws<ArgumentNullException>(() => { _ = meta.ObjectReference(); });
    }

    // ---- NamespacedName from V1ObjectReference ----------------------------------

    [Fact]
    public void NamespacedName_FromReference_UsesNamespaceAndName()
    {
        var reference = new V1ObjectReference { Name = "web", NamespaceProperty = "prod" };

        var nsName = reference.NamespacedName();

        Assert.Equal("prod", nsName.Namespace);
        Assert.Equal("web", nsName.Name);
    }

    [Fact]
    public void NamespacedName_FromNullReference_ReturnsEmptyLike()
    {
        V1ObjectReference? reference = null;

        var nsName = reference.NamespacedName();

        Assert.Equal(string.Empty, nsName.Namespace);
        Assert.Equal(string.Empty, nsName.Name);
    }

    [Fact]
    public void NamespacedName_FromReference_RoundTripsThroughObjectReference()
    {
        var pod = BuildPod();

        var nsName = pod.ObjectReference().NamespacedName();

        Assert.Equal("prod", nsName.Namespace);
        Assert.Equal("web", nsName.Name);
    }
}
