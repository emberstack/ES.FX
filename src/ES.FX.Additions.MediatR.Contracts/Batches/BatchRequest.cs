using JetBrains.Annotations;
using MediatR;

namespace ES.FX.Additions.MediatR.Contracts.Batches;

/// <summary>
///     A request containing a batch of items.
/// </summary>
/// <typeparam name="T">The type of items in the batch.</typeparam>
[PublicAPI]
public record BatchRequest<T> : IRequest
{
    /// <summary>
    ///     An enumerable collection of items in the batch.
    /// </summary>
    public IEnumerable<T> Items { get; set; } = [];
}