using JetBrains.Annotations;

namespace ES.FX.Additions.MassTransit.RabbitMQ.Configuration;

[PublicAPI]
public class RabbitMqOptions
{
    public string Host { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
}