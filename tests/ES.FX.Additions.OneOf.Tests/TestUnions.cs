using ES.FX.Additions.OneOf.Problems;
using ES.FX.Additions.OneOf.Types;
using ES.FX.Problems;
using OneOf;

// NOTE: These unions deliberately live in a namespace that does NOT descend from
// 'ES.FX.Additions.OneOf'. The OneOf source generator emits unqualified 'OneOf<...>' references,
// which would otherwise bind to the 'ES.FX.Additions.OneOf' namespace instead of the OneOf library
// and fail to compile. Keeping them isolated here avoids that collision.
namespace OneOfBridge.Tests.Unions;

/// <summary>
///     A discriminated union whose cases include a <see cref="Problem" /> in a middle slot.
///     Implements <see cref="IOneOfWithProblem" /> so the slot-agnostic bridge applies.
/// </summary>
[GenerateOneOf]
public partial class ResultOrProblem : OneOfBase<string, Problem, int>, IOneOfWithProblem;

/// <summary>
///     A union that declares <see cref="IOneOfWithProblem" /> but never actually holds a
///     <see cref="Problem" /> in the current instance — used to prove the negative path.
/// </summary>
[GenerateOneOf]
public partial class ValueOrFault : OneOfBase<string, Fault>, IOneOfWithProblem;
