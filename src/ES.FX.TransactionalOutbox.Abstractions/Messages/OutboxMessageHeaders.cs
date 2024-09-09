namespace ES.FX.TransactionalOutbox.Abstractions.Messages;

/// <summary>
///     Headers used in the outbox message
/// </summary>
public static class OutboxMessageHeaders
{
    private const string Prefix = "X-ES-FX-Outbox";

    /// <summary>
    ///     Diagnostics headers
    /// </summary>
    public static class Diagnostics
    {
        private const string DiagnosticsPrefix = $"{Prefix}-Diagnostics";

        /// <summary>
        ///     The activity id from which the message was sent
        /// </summary>
        public const string ActivityId = $"{DiagnosticsPrefix}-ActivityId";
    }

    /// <summary>
    ///     Message related headers
    /// </summary>
    public static class Message
    {
        private const string MessagePrefix = $"{Prefix}-Message";

        /// <summary>
        ///     Provides a hint about the type of the payload. Usually this is the assembly qualified name of the payload type as
        ///     originally serialized
        /// </summary>
        public const string PayloadOriginalType = $"{MessagePrefix}-Payload-Type-Original";
    }
}