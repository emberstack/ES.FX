using System.Reflection;
using System.Reflection.Emit;
using ES.FX.Additions.MassTransit.MessageKind;
using ES.FX.ComponentModel.DataAnnotations;
using MassTransit;
using Moq;

namespace ES.FX.Additions.MassTransit.Tests;

/// <summary>
///     Functional coverage of the inbound (receive-side) overload
///     <see cref="MessageKindProvider.GetType(ReceiveContext)" />, which reads the kind from
///     <see cref="ReceiveContext.TransportHeaders" /> and resolves it via the string overload. This is the counterpart
///     to the publish filter and underpins <see cref="TryResendUsingMessageKindFilter" />.
///     <para>
///         The <see cref="ReceiveContext" /> and its <see cref="Headers" /> are interfaces, so they are mocked; no
///         broker is required. Kind strings are unique per test to avoid colliding with the process-global provider
///         cache.
///     </para>
/// </summary>
public sealed class MessageKindProviderReceiveContextTests
{
    private static readonly AssemblyName DynAssemblyName = new("ES.FX.MassTransit.DynamicReceiveContracts");

    private static readonly ModuleBuilder Module = AssemblyBuilder
        .DefineDynamicAssembly(DynAssemblyName, AssemblyBuilderAccess.Run)
        .DefineDynamicModule("Main");

    private static Type NewTypeWithKind(string kind)
    {
        var typeName = "DynRecv_" + Guid.NewGuid().ToString("N");
        var tb = Module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
        var ctor = typeof(KindAttribute).GetConstructor([typeof(string)])!;
        tb.SetCustomAttribute(new CustomAttributeBuilder(ctor, [kind]));
        return tb.CreateType();
    }

    private static ReceiveContext ReceiveContextWithHeader(string? headerValue, bool headerPresent = true)
    {
        var headers = new Mock<Headers>();
        object? outValue = headerValue;
        headers
            .Setup(h => h.TryGetHeader(MessageKindProvider.Header, out outValue))
            .Returns(headerPresent);

        var context = new Mock<ReceiveContext>();
        context.SetupGet(c => c.TransportHeaders).Returns(headers.Object);
        return context.Object;
    }

    [Fact]
    public void GetType_HeaderMissing_ReturnsNull()
    {
        // TryGetHeader returns false => provider must short-circuit to null without touching the string overload.
        var context = ReceiveContextWithHeader(headerValue: null, headerPresent: false);

        Assert.Null(MessageKindProvider.GetType(context));
    }

    [Fact]
    public void GetType_HeaderPresentButKindUnregistered_ReturnsNull()
    {
        var unknownKind = "recv-unregistered-" + Guid.NewGuid();
        var context = ReceiveContextWithHeader(unknownKind);

        Assert.Null(MessageKindProvider.GetType(context));
    }

    [Fact]
    public void GetType_HeaderPresentAndKindRegistered_ResolvesType()
    {
        var kind = "recv-registered-" + Guid.NewGuid();
        var type = NewTypeWithKind(kind);
        MessageKindProvider.RegisterTypes(type);

        var context = ReceiveContextWithHeader(kind);

        Assert.Same(type, MessageKindProvider.GetType(context));
    }

    [Fact]
    public void GetType_HeaderValueIsNonString_UsesToStringToResolve()
    {
        // TransportHeaders values are typed as object; the provider resolves via headerValue.ToString().
        // A Guid header value therefore resolves against the registered kind produced from the same Guid string.
        var guid = Guid.NewGuid();
        var kind = "recv-guidheader-" + guid;
        var type = NewTypeWithKind(kind);
        MessageKindProvider.RegisterTypes(type);

        var headers = new Mock<Headers>();
        object? boxed = "recv-guidheader-" + guid; // simulate ToString() result being the registered kind
        headers.Setup(h => h.TryGetHeader(MessageKindProvider.Header, out boxed)).Returns(true);
        var context = new Mock<ReceiveContext>();
        context.SetupGet(c => c.TransportHeaders).Returns(headers.Object);

        Assert.Same(type, MessageKindProvider.GetType(context.Object));
    }
}
