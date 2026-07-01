using JetBrains.Annotations;

namespace ES.FX.Additions.MassTransit.RabbitMQ.Configuration;

/// <summary>
///     Plain options type for binding RabbitMQ connection settings from configuration, to pass into MassTransit's
///     RabbitMQ host configuration
/// </summary>
[PublicAPI]
public class RabbitMqOptions
{
    /// <summary>
    ///     The RabbitMQ host address
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    ///     The username used to authenticate with the RabbitMQ host
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    ///     The password used to authenticate with the RabbitMQ host
    /// </summary>
    public string? Password { get; set; }
}