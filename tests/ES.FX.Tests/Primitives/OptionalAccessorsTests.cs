using ES.FX.Primitives;

namespace ES.FX.Tests.Primitives;

public class OptionalAccessorsTests
{
    // ---- Value ----

    [Fact]
    public void Value_WhenSome_ReturnsWrappedValue()
    {
        var optional = Optional<int>.From(42);
        Assert.Equal(42, optional.Value);
    }

    [Fact]
    public void Value_WhenSomeNull_ReturnsNull()
    {
        var optional = Optional<string?>.From(null);
        Assert.Null(optional.Value);
    }

    [Fact]
    public void Value_WhenNone_Throws()
    {
        var optional = Optional<int>.None();
        Assert.Throws<InvalidOperationException>(() => optional.Value);
    }

    // ---- GetValueOrDefault() ----

    [Fact]
    public void GetValueOrDefault_WhenSome_ReturnsValue()
    {
        Assert.Equal(42, Optional<int>.From(42).GetValueOrDefault());
    }

    [Fact]
    public void GetValueOrDefault_WhenNone_ReturnsTypeDefault()
    {
        Assert.Equal(0, Optional<int>.None().GetValueOrDefault());
        Assert.Null(Optional<string>.None().GetValueOrDefault());
    }

    // ---- GetValueOrDefault(fallback) ----

    [Fact]
    public void GetValueOrDefault_Fallback_WhenSome_ReturnsValue()
    {
        Assert.Equal(42, Optional<int>.From(42).GetValueOrDefault(99));
    }

    [Fact]
    public void GetValueOrDefault_Fallback_WhenNone_ReturnsFallback()
    {
        Assert.Equal(99, Optional<int>.None().GetValueOrDefault(99));
    }

    // ---- Match ----

    [Fact]
    public void Match_WhenSome_InvokesWhenSome()
    {
        var optional = Optional<int>.From(42);
        var result = optional.Match(v => $"some:{v}", () => "none");
        Assert.Equal("some:42", result);
    }

    [Fact]
    public void Match_WhenNone_InvokesWhenNone()
    {
        var optional = Optional<int>.None();
        var result = optional.Match(v => $"some:{v}", () => "none");
        Assert.Equal("none", result);
    }

    [Fact]
    public void Match_WhenSomeNull_PassesNullToWhenSome()
    {
        var optional = Optional<string?>.From(null);
        var result = optional.Match(v => v is null ? "was-null" : "had-value", () => "none");
        Assert.Equal("was-null", result);
    }

    // ---- TryGetValue ----

    [Fact]
    public void TryGetValue_WhenSome_ReturnsTrue_AndValue()
    {
        var optional = Optional<int>.From(42);
        Assert.True(optional.TryGetValue(out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValue_WhenNone_ReturnsFalse_AndDefault()
    {
        var optional = Optional<int>.None();
        Assert.False(optional.TryGetValue(out var value));
        Assert.Equal(0, value);
    }
}