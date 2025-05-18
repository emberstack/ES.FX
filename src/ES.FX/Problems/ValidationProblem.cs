namespace ES.FX.Problems;

/// <summary>
///     A <see cref="Problem" /> that represents one or more validation errors.
/// </summary>
public record ValidationProblem : Problem
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ValidationProblem" /> class.
    /// </summary>
    public ValidationProblem() : this(new Dictionary<string, string[]>())
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ValidationProblem" /> class with the specified validation errors.
    /// </summary>
    /// <param name="errors">
    ///     A dictionary of validation errors where the key is the field name
    ///     and the value is an array of associated error messages.
    /// </param>
    public ValidationProblem(IDictionary<string, string[]> errors)
        : base(
            "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            "One or more validation errors occurred.") =>
        Errors = errors;

    /// <summary>
    ///     A dictionary of validation errors where the key is the field name
    ///     and the value is an array of associated error messages.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; set; } = new Dictionary<string, string[]>();
}