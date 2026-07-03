using ES.FX.Additions.MediatR.Contracts.Batches;
using MediatR;

namespace ES.FX.Additions.MediatR.Contracts.Tests;

public class BatchRequestTests
{
    public sealed record Person(string Name, int Age);

    [Fact]
    public void BatchRequest_ImplementsIRequest()
    {
        Assert.IsAssignableFrom<IRequest>(new BatchRequest<int>());
        // The parameterless IRequest carries the IBaseRequest marker MediatR uses to
        // route the message; the closed BatchRequest<T> must satisfy it.
        Assert.IsAssignableFrom<IBaseRequest>(new BatchRequest<int>());
    }

    [Fact]
    public void Items_DefaultsToEmptyNonNullList()
    {
        var request = new BatchRequest<string>();

        Assert.NotNull(request.Items);
        Assert.Empty(request.Items);
    }

    [Fact]
    public void Items_RoundTripsPayload()
    {
        var people = new List<Person>
        {
            new("Ada", 36),
            new("Alan", 41)
        };

        var request = new BatchRequest<Person> { Items = people };

        Assert.Equal(2, request.Items.Count);
        Assert.Equal(people[0], request.Items[0]);
        Assert.Equal(people[1], request.Items[1]);
        Assert.IsAssignableFrom<IReadOnlyList<Person>>(request.Items);
    }

    [Fact]
    public void WithExpression_ProducesNewInstance_LeavingOriginalUnchanged()
    {
        var original = new BatchRequest<int> { Items = [1, 2, 3] };
        var updated = original with { Items = [9, 9] };

        Assert.Equal([1, 2, 3], original.Items);
        Assert.Equal([9, 9], updated.Items);
        Assert.NotSame(original, updated);
    }

    [Fact]
    public void RecordEquality_ComparesByReferenceOfItems_NotStructurally()
    {
        // IReadOnlyList<T> uses reference equality in the generated record equality,
        // so two distinct lists with equal content are NOT equal. This documents
        // the real current behavior.
        var items = new[] { 1, 2, 3 };
        var a = new BatchRequest<int> { Items = items };
        var b = new BatchRequest<int> { Items = items };
        var c = new BatchRequest<int> { Items = [1, 2, 3] };

        Assert.Equal(a, b);        // same list reference => equal
        Assert.NotEqual(a, c);     // different list reference => not equal
    }

    [Fact]
    public async Task SatisfiesRequestHandlerConstraint_AndHandlerReceivesItems()
    {
        // IRequestHandler<TRequest> has a `where TRequest : IRequest` constraint.
        // The mere existence of SumHandler proves BatchRequest<int> satisfies it at
        // compile time; here we exercise the handler to confirm Items flow through.
        var handler = new SumHandler();
        var request = new BatchRequest<int> { Items = [10, 20, 30] };

        await handler.Handle(request, TestContext.Current.CancellationToken);

        Assert.Equal([10, 20, 30], SumHandler.LastSeenItems);
    }

    [Fact]
    public void ClosedGeneric_ExposesIRequestOfUnitContract()
    {
        // MediatR routes parameterless requests as IRequest<Unit> internally.
        // Reflection proves the closed type carries that closed interface even
        // though the C# `is`/assignability check above only sees IRequest.
        var iface = typeof(BatchRequest<int>).GetInterfaces();

        Assert.Contains(iface, i => i == typeof(IRequest));
        Assert.Contains(iface, i => i == typeof(IBaseRequest));
    }

    // where TRequest : IRequest — will not compile if BatchRequest<T> stops being IRequest.
    private sealed class SumHandler : IRequestHandler<BatchRequest<int>>
    {
        public static IReadOnlyList<int> LastSeenItems { get; private set; } = [];

        public Task Handle(BatchRequest<int> request, CancellationToken cancellationToken)
        {
            LastSeenItems = request.Items;
            return Task.CompletedTask;
        }
    }
}
