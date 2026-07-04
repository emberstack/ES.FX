using ES.FX.Additions.OneOf.Types;

namespace ES.FX.Additions.OneOf.Tests;

/// <summary>
///     Functional regression coverage for the non-generic and generic case types:
///     value equality, payload access via <c>.Value</c>, and deconstruction.
/// </summary>
public class CaseTypeTests
{
    // ----- Non-generic markers: value equality (all instances of a case are equal) -----

    [Fact]
    public void Failure_DefaultInstances_AreEqual()
    {
        var a = new Failure();
        var b = new Failure();

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Fault_DefaultInstances_AreEqual()
    {
        Assert.Equal(new Fault(), new Fault());
        Assert.True(new Fault() == default);
    }

    [Fact]
    public void Fatal_DefaultInstances_AreEqual() => Assert.Equal(new Fatal(), default);

    [Fact]
    public void Canceled_DefaultInstances_AreEqual() => Assert.Equal(new Canceled(), default);

    [Fact]
    public void TimedOut_DefaultInstances_AreEqual() => Assert.Equal(new TimedOut(), default);

    [Fact]
    public void Deferred_DefaultInstances_AreEqual() => Assert.Equal(new Deferred(), default);

    [Fact]
    public void InProgress_DefaultInstances_AreEqual() => Assert.Equal(new InProgress(), default);

    [Fact]
    public void Interrupted_DefaultInstances_AreEqual() => Assert.Equal(new Interrupted(), default);

    [Fact]
    public void Unknown_DefaultInstances_AreEqual() => Assert.Equal(new Unknown(), default);

    [Fact]
    public void DistinctNonGenericCaseTypes_AreNotInterchangeable()
    {
        // Distinct case types are distinct CLR types; boxed, they are never equal.
        object failure = new Failure();
        object fault = new Fault();

        Assert.NotEqual(failure, fault);
    }

    // ----- Generic payload cases: .Value access -----

    [Fact]
    public void FailureOfT_ExposesPayload_ViaValue()
    {
        var failure = new Failure<string>("boom");

        Assert.Equal("boom", failure.Value);
    }

    [Fact]
    public void FaultOfT_ExposesPayload_ViaValue()
    {
        var fault = new Fault<int>(42);

        Assert.Equal(42, fault.Value);
    }

    [Fact]
    public void TimedOutOfT_ExposesPayload_ViaValue()
    {
        var timedOut = new TimedOut<TimeSpan>(TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), timedOut.Value);
    }

    [Fact]
    public void CanceledOfT_ExposesPayload_ViaValue()
    {
        var payload = new object();
        var canceled = new Canceled<object>(payload);

        Assert.Same(payload, canceled.Value);
    }

    // ----- Generic payload cases: value equality is payload-driven -----

    [Fact]
    public void GenericCase_WithSamePayload_AreEqual()
    {
        var a = new Failure<string>("same");
        var b = new Failure<string>("same");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GenericCase_WithDifferentPayload_AreNotEqual()
    {
        var a = new Fatal<int>(1);
        var b = new Fatal<int>(2);

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    // ----- Generic payload cases: pattern matching on the positional payload -----

    [Fact]
    public void GenericCase_SupportsPositionalPatternMatch()
    {
        var timedOut = new TimedOut<string>("deadline exceeded");

        // Positional record structs expose their payload for property/positional patterns.
        var matched = timedOut is { Value: "deadline exceeded" };

        Assert.True(matched);
    }

    [Fact]
    public void GenericCase_PayloadIsReadableAfterConstruction()
    {
        var deferred = new Deferred<int>(7);

        Assert.Equal(7, deferred.Value);
    }

    // ----- with-expression mutation semantics on record struct -----

    [Fact]
    public void GenericCase_WithExpression_ProducesModifiedCopy()
    {
        var original = new Interrupted<string>("first");

        var modified = original with { Value = "second" };

        Assert.Equal("first", original.Value);
        Assert.Equal("second", modified.Value);
        Assert.NotEqual(original, modified);
    }

    [Fact]
    public void GenericCase_ToString_IncludesPayload()
    {
        var unknown = new Unknown<string>("mystery");

        // Positional record struct ToString surfaces the member name and value.
        var text = unknown.ToString();

        Assert.Contains("mystery", text);
        Assert.Contains("Value", text);
    }

    [Fact]
    public void GenericCase_SupportsNullReferencePayload()
    {
        var canceled = new Canceled<string?>(null);

        Assert.Null(canceled.Value);
        Assert.Equal(new Canceled<string?>(null), canceled);
    }
}