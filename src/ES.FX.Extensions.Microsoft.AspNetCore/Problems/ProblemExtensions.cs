using System.Net;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    ///     These properties are not included in the extensions dictionary of the ProblemDetails object. They are included as
    ///     top-level properties.
    /// </summary>
    private static readonly HashSet<string> PropertiesToSkip = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Problem.Detail),
        nameof(Problem.Title),
        nameof(Problem.Type),
        nameof(Problem.Instance),
        nameof(Problem.Status)
    };

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

        Dictionary<string, object?> extensions;
        try
        {
            //Serialize and deserialize to ensure that the object is JSON safe. This also sets the correct casing for the keys and properties.
            var jsonSafeExtensions = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                JsonSerializer.Serialize(problem as object, JsonSerializerOptions), JsonSerializerOptions);
            extensions = jsonSafeExtensions?
                             .Where(kvp => !PropertiesToSkip.Contains(kvp.Key))
                             .Select(x => new KeyValuePair<string, object?>(x.Key, x.Value))
                             .ToDictionary(x => x.Key, x => x.Value)
                         ?? [];
        }
        catch
        {
            extensions = [];
        }


        return new ProblemDetails
        {
            Type = problem.Type,
            Detail = problem.Detail ?? ReasonPhrases.GetReasonPhrase(statusCode),
            Status = statusCode,
            Title = problem.Title,
            Instance = problem.Instance,
            Extensions = extensions
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