using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using ES.FX.Problems;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using ValidationProblem = ES.FX.Problems.ValidationProblem;

namespace ES.FX.Extensions.Microsoft.AspNetCore.Problems;

public static class ProblemExtensions
{
    // Cache the ProblemDetails properties (excluding Extensions) once.
    private static readonly HashSet<string> ProblemDetailsProperties = new(
        typeof(ProblemDetails)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(name => !string.Equals(name, nameof(ProblemDetails.Extensions), StringComparison.OrdinalIgnoreCase)),
        StringComparer.OrdinalIgnoreCase);

    // Cache the properties for Problem types so reflection occurs only once
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ProblemPropertiesCache = new();

    /// <summary>
    ///     Returns a <see cref="BadRequest{ProblemDetails}" /> result with the specified <see cref="Problem" />
    /// </summary>
    [PublicAPI]
    public static BadRequest<ProblemDetails> AsBadRequestResult(this Problem problem) =>
        TypedResults.BadRequest(problem.AsProblemDetails());

    /// <summary>
    ///     Returns a <see cref="Conflict{ProblemDetails}" /> result with the specified <see cref="Problem" />
    /// </summary>
    [PublicAPI]
    public static Conflict<ProblemDetails> AsConflictResult(this Problem problem) =>
        TypedResults.Conflict(problem.AsProblemDetails());

    /// <summary>
    ///     Returns an <see cref="Ok{ProblemDetails}" /> result with the specified <see cref="Problem" />
    /// </summary>
    [PublicAPI]
    public static Ok<ProblemDetails> AsOkResult(this Problem problem) =>
        TypedResults.Ok(problem.AsProblemDetails());


    /// <summary>
    ///     Formats a <see cref="Problem" /> as <see cref="ProblemDetails" />
    /// </summary>
    /// <param name="problem">The source <see cref="Problem" /></param>
    [PublicAPI]
    public static ProblemDetails AsProblemDetails(this Problem problem)
    {
        var statusCode = problem.Status ?? (problem is ValidationProblem
            ? (int)HttpStatusCode.BadRequest
            : (int)HttpStatusCode.UnprocessableEntity);

        var problemType = problem.GetType();
        var properties = ProblemPropertiesCache.GetOrAdd(problemType, type =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && !ProblemDetailsProperties.Contains(p.Name))
                .ToArray());

        return new ProblemDetails
        {
            Type = problem.Type,
            Detail = problem.Detail ?? ReasonPhrases.GetReasonPhrase(statusCode),
            Status = statusCode,
            Title = problem.Title,
            Instance = problem.Instance,
            Extensions = properties.ToDictionary(p => p.Name, p => p.GetValue(problem))
        };
    }


    /// <summary>
    ///     Returns a <see cref="ProblemHttpResult" /> result with the specified <see cref="Problem" />
    /// </summary>
    [PublicAPI]
    public static ProblemHttpResult AsProblemResult(this Problem problem) =>
        TypedResults.Problem(problem.AsProblemDetails());

    /// <summary>
    ///     Returns an <see cref="UnprocessableEntity{ProblemDetails}" /> result with the specified <see cref="Problem" />
    /// </summary>
    [PublicAPI]
    public static UnprocessableEntity<ProblemDetails> AsUnprocessableEntityResult(this Problem problem) =>
        TypedResults.UnprocessableEntity(problem.AsProblemDetails());
}