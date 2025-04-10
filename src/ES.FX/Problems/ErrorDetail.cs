using JetBrains.Annotations;

namespace ES.FX.Problems
{

    /// <summary>
    ///     Represents an error detail with a message and an error code.
    /// </summary>
    [PublicAPI] // Marking this class as part of a public API
    public class ErrorDetail
    {
        /// <summary>
        ///     The validation error message.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        ///     The validation error code.
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;
    }
}