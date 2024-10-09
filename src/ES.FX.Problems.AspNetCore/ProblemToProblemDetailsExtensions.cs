using System.Net;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;

namespace ES.FX.Problems.AspNetCore;

public static class ProblemToProblemDetailsExtensions
{
    [PublicAPI]
    public static ProblemDetails ToProblemDetails(this Problem problem)
    {
        var propertiesToSkip = new HashSet<string>
        {
            nameof(Problem.Detail),
            nameof(Problem.Title),
            nameof(Problem.Type),
            nameof(Problem.Instance)
        };

        return new ProblemDetails
        {
            Detail = problem.Detail,
            Status = problem is ValidationProblem
                ? (int)HttpStatusCode.BadRequest
                : (int)HttpStatusCode.PreconditionFailed,
            Title = problem.Title,
            Type = problem.Type,
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