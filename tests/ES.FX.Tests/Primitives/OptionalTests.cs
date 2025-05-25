using ES.FX.Primitives;

namespace ES.FX.Tests.Primitives;

public class OptionalTests
{
    [Fact]
    public void Optional_Reference_Nullable_Can_HaveNullValue()
    {
        var a = Optional<string?>.From(null);
        Assert.True(a.HasValue);
    }

    [Fact]
    public void Optional_Reference_Nullable_Can_HaveValue()
    {
        var a = Optional<string?>.From(string.Empty);
        Assert.True(a.HasValue);
    }

    [Fact]
    public void Optional_Reference_Nullable_Can_BeNone()
    {
        var a = Optional<string?>.None();
        Assert.False(a.HasValue);
    }

    [Fact]
    public void Optional_Reference_Can_HaveValue()
    {
        var a = Optional<string>.From(string.Empty);
        Assert.True(a.HasValue);
    }

    [Fact]
    public void Optional_Reference_Can_BeNone()
    {
        var a = Optional<string>.None();
        Assert.False(a.HasValue);
    }


    [Fact]
    public void Optional_Value_Nullable_Can_HaveNullValue()
    {
        var a = Optional<int?>.From(null);
        Assert.True(a.HasValue);
    }

    [Fact]
    public void Optional_Value_Nullable_Can_HaveValue()
    {
        var a = Optional<int?>.From(10);
        Assert.True(a.HasValue);
    }

    [Fact]
    public void Optional_Value_Nullable_Can_BeNone()
    {
        var a = Optional<int?>.None();
        Assert.False(a.HasValue);
    }

    [Fact]
    public void Optional_Value_Can_HaveValue()
    {
        var a = Optional<int>.From(10);
        Assert.True(a.HasValue);
    }

    [Fact]
    public void Optional_Value_Can_BeNone()
    {
        var a = Optional<int>.None();
        Assert.False(a.HasValue);
    }
}