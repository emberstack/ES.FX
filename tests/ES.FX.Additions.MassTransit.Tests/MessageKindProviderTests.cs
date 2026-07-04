using System.Reflection;
using System.Reflection.Emit;
using ES.FX.Additions.MassTransit.MessageKind;
using ES.FX.ComponentModel.DataAnnotations;

namespace ES.FX.Additions.MassTransit.Tests;

/// <summary>
///     Functional coverage of <see cref="MessageKindProvider" />.
///     <para>
///         IMPORTANT: <see cref="MessageKindProvider" /> stores its kind-&gt;type map in a process-global static
///         <c>ConcurrentDictionary</c> with no public reset API. To keep tests independent, every test that registers
///         a kind uses a unique kind string (a <see cref="Guid" />) and, where a distinct .NET type is required,
///         emits a fresh runtime type carrying a <see cref="KindAttribute" />. This guarantees no collision with
///         types registered by other tests or by the harness-based tests in this assembly.
///     </para>
/// </summary>
public sealed class MessageKindProviderTests
{
    private static readonly AssemblyName DynAssemblyName = new("ES.FX.MassTransit.DynamicContracts");

    private static readonly ModuleBuilder Module = AssemblyBuilder
        .DefineDynamicAssembly(DynAssemblyName, AssemblyBuilderAccess.Run)
        .DefineDynamicModule("Main");

    /// <summary>
    ///     Emits a brand-new runtime class annotated with <c>[Kind(kind)]</c>. Each call produces a distinct
    ///     <see cref="Type" />, so tests can create real kind-&gt;type collisions without touching source contracts.
    /// </summary>
    private static Type NewTypeWithKind(string kind)
    {
        var typeName = "DynMsg_" + Guid.NewGuid().ToString("N");
        var tb = Module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);

        var ctor = typeof(KindAttribute).GetConstructor([typeof(string)])!;
        tb.SetCustomAttribute(new CustomAttributeBuilder(ctor, [kind]));

        return tb.CreateType();
    }

    [Fact]
    public void Header_HasExpectedStableValue()
    {
        // This header name is a wire contract shared with consumers; a change would silently break interop.
        Assert.Equal("X-ES-FX-Kind", MessageKindProvider.Header);
    }

    [Fact]
    public void GetType_NullKind_ReturnsNull()
    {
        Assert.Null(MessageKindProvider.GetType((string?)null));
    }

    [Fact]
    public void GetType_UnregisteredKind_ReturnsNull()
    {
        Assert.Null(MessageKindProvider.GetType("kind-that-was-never-registered-" + Guid.NewGuid()));
    }

    [Fact]
    public void RegisterTypes_ThenGetType_ResolvesRegisteredType()
    {
        var kind = "roundtrip-" + Guid.NewGuid();
        var type = NewTypeWithKind(kind);

        MessageKindProvider.RegisterTypes(type);

        Assert.Same(type, MessageKindProvider.GetType(kind));
    }

    [Fact]
    public void RegisterTypes_TypeWithoutKindAttribute_IsIgnored()
    {
        // PlainMessage has no [Kind]; registering it must be a no-op (nothing to key on).
        MessageKindProvider.RegisterTypes(typeof(PlainMessage));

        // Nothing resolves back to it, and it does not throw.
        Assert.Null(KindAttribute.For(typeof(PlainMessage)));
    }

    [Fact]
    public void RegisterTypes_SameKindSameType_IsIdempotent()
    {
        var kind = "idempotent-" + Guid.NewGuid();
        var type = NewTypeWithKind(kind);

        MessageKindProvider.RegisterTypes(type);
        // Re-registering the exact same (kind -> same type) mapping must be fine.
        MessageKindProvider.RegisterTypes(type);
        MessageKindProvider.RegisterTypes(type, type);

        Assert.Same(type, MessageKindProvider.GetType(kind));
    }

    /// <summary>
    ///     Registering the SAME kind against a DIFFERENT .NET type.
    ///     <para>
    ///         NOTE ON CURRENT BEHAVIOR: the task brief stated <c>RegisterTypes</c> should FAIL FAST
    ///         (throw <see cref="InvalidOperationException" />) on a kind-&gt;different-type collision. The library
    ///         as it currently stands (<c>MessageKindProvider.RegisterTypes</c> uses <c>ConcurrentDictionary.TryAdd</c>)
    ///         does NOT throw — it silently keeps the FIRST registration and ignores the later, conflicting one
    ///         ("first registration wins"). Per the testing rules, this test asserts the CURRENT real behavior and
    ///         documents the divergence rather than editing the library. If the fail-fast change lands, this test
    ///         must be updated to expect the throw.
    ///     </para>
    /// </summary>
    [Fact]
    public void RegisterTypes_SameKindDifferentType_CurrentlyKeepsFirstAndDoesNotThrow()
    {
        var kind = "collision-" + Guid.NewGuid();
        var first = NewTypeWithKind(kind);
        var second = NewTypeWithKind(kind);

        MessageKindProvider.RegisterTypes(first);

        // Current library behavior: no throw; first-wins.
        var exception = Record.Exception(() => MessageKindProvider.RegisterTypes(second));
        Assert.Null(exception);

        Assert.Same(first, MessageKindProvider.GetType(kind));
        Assert.NotSame(second, MessageKindProvider.GetType(kind));
    }

    [Fact]
    public void RegisterTypes_MixedBatch_RegistersKindedTypesAndSkipsUnkinded()
    {
        var kindA = "batch-a-" + Guid.NewGuid();
        var kindB = "batch-b-" + Guid.NewGuid();
        var typeA = NewTypeWithKind(kindA);
        var typeB = NewTypeWithKind(kindB);

        MessageKindProvider.RegisterTypes(typeA, typeof(PlainMessage), typeB);

        Assert.Same(typeA, MessageKindProvider.GetType(kindA));
        Assert.Same(typeB, MessageKindProvider.GetType(kindB));
    }

    [Fact]
    public void RegisterTypes_EmptyArgs_DoesNotThrow()
    {
        var exception = Record.Exception(() => MessageKindProvider.RegisterTypes());
        Assert.Null(exception);
    }
}