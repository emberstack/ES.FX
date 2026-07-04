using ES.FX.Additions.MediatR.Contracts.Batches;
using MediatR;

namespace ES.FX.Additions.MediatR.Tests;

/// <summary>
///     Guards the shape and record semantics of the public batch contract types.
///     These are the entire public surface re-exported by the ES.FX.Additions.MediatR package.
/// </summary>
public class BatchContractTests
{
    [Fact]
    public void BatchRequest_ImplementsIRequest()
    {
        Assert.IsAssignableFrom<IRequest>(new BatchRequest<int>());
        // Non-generic IRequest is the marker used by IRequestHandler with no response.
        Assert.IsAssignableFrom<IBaseRequest>(new BatchRequest<int>());
    }

    [Fact]
    public void BatchNotification_ImplementsINotification()
    {
        Assert.IsAssignableFrom<INotification>(new BatchNotification<int>());
    }

    [Fact]
    public void BatchRequest_DefaultItems_IsEmptyNonNullList()
    {
        var request = new BatchRequest<string>();
        Assert.NotNull(request.Items);
        Assert.Empty(request.Items);
    }

    [Fact]
    public void BatchNotification_DefaultItems_IsEmptyNonNullList()
    {
        var notification = new BatchNotification<string>();
        Assert.NotNull(notification.Items);
        Assert.Empty(notification.Items);
    }

    [Fact]
    public void BatchRequest_Items_IsReadOnlyList()
    {
        var request = new BatchRequest<int> { Items = [1, 2, 3] };
        Assert.IsAssignableFrom<IReadOnlyList<int>>(request.Items);
        Assert.Equal([1, 2, 3], request.Items);
        Assert.Equal(3, request.Items.Count);
    }

    [Fact]
    public void BatchNotification_Items_PreservesOrderAndValues()
    {
        var notification = new BatchNotification<string> { Items = ["a", "b", "c"] };
        Assert.Equal(["a", "b", "c"], notification.Items);
    }

    [Fact]
    public void BatchRequest_ValueEquality_SameReferenceItems_AreEqual()
    {
        // Records compare by member reference for reference-typed members (the IReadOnlyList).
        // Two records built from the SAME list instance are equal.
        var items = new List<int> { 1, 2, 3 };
        var a = new BatchRequest<int> { Items = items };
        var b = new BatchRequest<int> { Items = items };

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void BatchRequest_ValueEquality_DifferentListInstances_AreNotEqual()
    {
        // Record equality does NOT deep-compare the list; different list instances differ.
        // This documents/guards the CURRENT behavior of the record contract.
        var a = new BatchRequest<int> { Items = [1, 2, 3] };
        var b = new BatchRequest<int> { Items = [1, 2, 3] };

        Assert.NotEqual(a, b);
        Assert.False(a == b);
    }

    [Fact]
    public void BatchRequest_WithExpression_ProducesNewInstance_OriginalUnchanged()
    {
        var original = new BatchRequest<int> { Items = [1, 2] };
        var mutated = original with { Items = [9, 9, 9] };

        Assert.Equal([1, 2], original.Items);
        Assert.Equal([9, 9, 9], mutated.Items);
        Assert.NotSame(original, mutated);
    }

    [Fact]
    public void BatchNotification_WithExpression_ProducesNewInstance()
    {
        var original = new BatchNotification<string> { Items = ["x"] };
        var mutated = original with { Items = ["y", "z"] };

        Assert.Equal(["x"], original.Items);
        Assert.Equal(["y", "z"], mutated.Items);
    }

    [Fact]
    public void BatchRequest_SupportsComplexAndValueItemTypes()
    {
        var complex = new BatchRequest<(int Id, string Name)>
        {
            Items = [(1, "one"), (2, "two")]
        };

        Assert.Equal(2, complex.Items.Count);
        Assert.Equal("two", complex.Items[1].Name);
    }
}