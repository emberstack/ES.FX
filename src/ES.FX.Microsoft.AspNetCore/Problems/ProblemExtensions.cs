using System.Net;
using System.Reflection;
using ES.FX.Problems;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;

namespace ES.FX.Microsoft.AspNetCore.Problems;

public static class ProblemExtensions
{
    [PublicAPI]
    public static ProblemDetails ToProblemDetails(this Problem problem)
    {
        var propertiesToSkip = new HashSet<string>
        {
            nameof(Problem.Detail),
            nameof(Problem.Title),
            nameof(Problem.Type),
            nameof(Problem.Instance),
            nameof(Problem.Status)
        };

        return new ProblemDetails
        {
            Type = problem.Type,
            Detail = problem.Detail,
            Status = problem.Status ?? (problem is ValidationProblem
                ? (int)HttpStatusCode.BadRequest
                : (int)HttpStatusCode.PreconditionFailed),
            Title = problem.Title,
            Instance = problem.Instance,
            Extensions = problem.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => prop.CanRead && !propertiesToSkip.Contains(prop.Name))
                .ToDictionary(
                    prop => prop.Name,
                    prop => prop.GetValue(problem))
        };
    }
}