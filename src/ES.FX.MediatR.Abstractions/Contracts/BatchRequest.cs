using JetBrains.Annotations;
using MediatR;

namespace ES.FX.MediatR.Abstractions.Contracts;

/// <summary>
/// A request containing a batch of items
/// </summary>
/// <typeparam name="T">Item type</typeparam>
/// <param name="Items">Enumerable of items</param>
[PublicAPI]
public record BatchRequest<T>(IEnumerable<T> Items) : IRequest;