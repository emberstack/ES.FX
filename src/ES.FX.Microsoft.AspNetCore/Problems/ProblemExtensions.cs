using System.Net;
using System.Text.Json;
using ES.FX.Problems;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace ES.FX.Microsoft.AspNetCore.Problems;

public static class ProblemExtensions
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// These properties are not included in the extensions dictionary of the ProblemDetails object. They are included as top-level properties.
    /// </summary>
    private static readonly HashSet<string> PropertiesToSkip = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Problem.Detail),
        nameof(Problem.Title),
        nameof(Problem.Type),
        nameof(Problem.Instance),
        nameof(Problem.Status)
    };

    [PublicAPI]
    public static ProblemDetails ToProblemDetails(this Problem problem)
    {
        var statusCode = problem.Status ?? (problem is ValidationProblem
            ? (int)HttpStatusCode.BadRequest
            : (int)HttpStatusCode.PreconditionFailed);

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
                         ?? new Dictionary<string, object?>();
        }
        catch
        {
            extensions = new Dictionary<string, object?>();
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
}