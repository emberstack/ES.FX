namespace ES.FX.Problems;

public record ValidationProblem(IDictionary<string, string[]> Errors) : 
    Problem("https://tools.ietf.org/html/rfc9110#section-15.5.1",
        "One or more validation errors occurred.");