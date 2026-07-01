using JetBrains.Annotations;
using MassTransit;

namespace ES.FX.Additions.MassTransit.MessageKind;

/// <summary>
///     Extension methods for enabling message kind support on a bus
/// </summary>
[PublicAPI]
public static class MessageKindExtensions
{
    /// <summary>
    ///     Enables message kind support on the bus. Connects a
    ///     <see cref="MessageKindConsumerConfigurationObserver" /> that registers the consumed message types with the
    ///     <see cref="MessageKindProvider" /> and adds a <see cref="MessageKindPublishFilter{T}" /> that annotates
    ///     outgoing messages with the kind header.
    ///     <para>
    ///         Note: <see cref="TryResendUsingMessageKindFilter" /> is not added by this method and must be wired
    ///         separately on the dead-letter pipe.
    ///     </para>
    /// </summary>
    /// <param name="cfg">The bus factory configurator</param>
    /// <param name="registrationContext">The registration context used to resolve the publish filter</param>
    public static void UseMessageKind(this IBusFactoryConfigurator cfg, IRegistrationContext registrationContext)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(registrationContext);

        //Monitor consumers to see what message types are needed
        cfg.ConnectConsumerConfigurationObserver(new MessageKindConsumerConfigurationObserver());

        //Add a filter to annotate the outgoing messages with the message type
        cfg.UsePublishFilter(typeof(MessageKindPublishFilter<>), registrationContext);
    }
}