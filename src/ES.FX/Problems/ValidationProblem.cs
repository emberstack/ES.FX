namespace ES.FX.Problems;

/// <summary>
///     A <see cref="Problem" /> that represents one or more validation errors.
/// </summary>
/// <param name="Errors"></param>
public record ValidationProblem(IDictionary<string, string[]> Errors) :
    Problem("https://tools.ietf.org/html/rfc9110#section-15.5.1",
        "One or more validation errors occurred.");