using JetBrains.Annotations;
using MediatR;

namespace ES.FX.MediatR.Contracts;

/// <summary>
/// A notification containing a batch of items
/// </summary>
/// <typeparam name="T">Item type</typeparam>
/// <param name="Items">Enumerable of items</param>
[PublicAPI]
public record BatchNotification<T>(IEnumerable<T> Items) : INotification;