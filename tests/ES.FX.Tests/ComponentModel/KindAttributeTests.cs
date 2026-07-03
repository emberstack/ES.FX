using ES.FX.ComponentModel.DataAnnotations;

namespace ES.FX.Tests.ComponentModel;

public class KindAttributeTests
{
    [Kind("decorated-kind")]
    private class DecoratedType;

    private class UndecoratedType;

    [FaultKind("decorated-fault")]
    private class DecoratedFaultType;

    private class UndecoratedFaultType;

    // ---- KindAttribute ----

    [Fact]
    public void Kind_For_DecoratedType_ReturnsKind()
    {
        Assert.Equal("decorated-kind", KindAttribute.For(typeof(DecoratedType)));
    }

    [Fact]
    public void Kind_ForGeneric_DecoratedType_ReturnsKind()
    {
        Assert.Equal("decorated-kind", KindAttribute.For<DecoratedType>());
    }

    [Fact]
    public void Kind_For_UndecoratedType_ReturnsNull()
    {
        Assert.Null(KindAttribute.For(typeof(UndecoratedType)));
    }

    [Fact]
    public void Kind_For_CachesResult_SecondLookupConsistent()
    {
        // First lookup populates the cache; second must return the same value from cache.
        var first = KindAttribute.For(typeof(DecoratedType));
        var second = KindAttribute.For(typeof(DecoratedType));
        Assert.Equal(first, second);
        Assert.Equal("decorated-kind", second);

        // Same for the null (undecorated) branch which also caches.
        Assert.Null(KindAttribute.For(typeof(UndecoratedType)));
        Assert.Null(KindAttribute.For(typeof(UndecoratedType)));
    }

    [Fact]
    public void Kind_Property_ExposesConstructorValue()
    {
        var attribute = new KindAttribute("some-kind");
        Assert.Equal("some-kind", attribute.Kind);
    }

    // ---- FaultKindAttribute ----

    [Fact]
    public void FaultKind_For_DecoratedType_ReturnsKind()
    {
        Assert.Equal("decorated-fault", FaultKindAttribute.For(typeof(DecoratedFaultType)));
    }

    [Fact]
    public void FaultKind_ForGeneric_DecoratedType_ReturnsKind()
    {
        Assert.Equal("decorated-fault", FaultKindAttribute.For<DecoratedFaultType>());
    }

    [Fact]
    public void FaultKind_For_UndecoratedType_ReturnsNull()
    {
        Assert.Null(FaultKindAttribute.For(typeof(UndecoratedFaultType)));
    }

    [Fact]
    public void FaultKind_For_CachesResult_SecondLookupConsistent()
    {
        var first = FaultKindAttribute.For(typeof(DecoratedFaultType));
        var second = FaultKindAttribute.For(typeof(DecoratedFaultType));
        Assert.Equal(first, second);
        Assert.Equal("decorated-fault", second);
    }
}
