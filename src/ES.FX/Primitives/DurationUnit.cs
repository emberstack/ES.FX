using JetBrains.Annotations;

namespace ES.FX.Primitives;

/// <summary>
///     Specifies units of duration for temporal measurements.
/// </summary>
/// <remarks>
///     <para>
///         The <see cref="DurationUnit" /> enumeration defines both fixed-duration units
///         (<see cref="Tick" /> through <see cref="Hour" />) and calendar-duration units
///         (<see cref="Day" /> through <see cref="Millennium" />).
///     </para>
///     <para>
///         Fixed-duration units correspond to <see cref="TimeSpan" /> properties (for example,
///         <c>TimeSpan.TicksPerSecond</c> for <see cref="Second" />). Calendar-duration units
///         require <see cref="DateTime" /> arithmetic (for example, <c>DateTime.AddMonths(int)</c>
///         for <see cref="Month" />).
///     </para>
/// </remarks>
/// <example>
///     <code language="csharp">
/// // Convert 3 units of the specified duration to a TimeSpan.
/// TimeSpan span;
/// switch (unit)
/// {
///     case DurationUnit.Hour:
///         span = TimeSpan.FromHours(3);
///         break;
///     case DurationUnit.Minute:
///         span = TimeSpan.FromMinutes(3);
///         break;
///     case DurationUnit.Second:
///         span = TimeSpan.FromSeconds(3);
///         break;
///     // Add additional cases as needed...
///     default:
///         throw new ArgumentOutOfRangeException(nameof(unit), unit, null);
/// }
/// Console.WriteLine(span);
/// </code>
/// </example>
/// <seealso cref="TimeSpan" />
/// <seealso cref="DateTime" />
[PublicAPI]
public enum DurationUnit
{
    /// <summary>
    ///     A 100-nanosecond interval.
    /// </summary>
    Tick,

    /// <summary>
    ///     One nanosecond (1×10⁻⁹ second). Mapping to ticks may require rounding.
    /// </summary>
    Nanosecond,

    /// <summary>
    ///     One microsecond (1×10⁻⁶ second). Mapping to ticks may require rounding.
    /// </summary>
    Microsecond,

    /// <summary>
    ///     One millisecond (1×10⁻³ second).
    /// </summary>
    Millisecond,

    /// <summary>
    ///     One second.
    /// </summary>
    Second,

    /// <summary>
    ///     One minute (60 seconds).
    /// </summary>
    Minute,

    /// <summary>
    ///     One hour (60 minutes).
    /// </summary>
    Hour,

    /// <summary>
    ///     One day (24 hours).
    /// </summary>
    Day,

    /// <summary>
    ///     One weekend (2 days).
    /// </summary>
    Weekend,

    /// <summary>
    ///     One week (7 days).
    /// </summary>
    Week,

    /// <summary>
    ///     One calendar month (28–31 days).
    /// </summary>
    Month,

    /// <summary>
    ///     One calendar quarter (3 months).
    /// </summary>
    Quarter,

    /// <summary>
    ///     One calendar year (12 months).
    /// </summary>
    Year,

    /// <summary>
    ///     One decade (10 years).
    /// </summary>
    Decade,

    /// <summary>
    ///     One century (100 years).
    /// </summary>
    Century,

    /// <summary>
    ///     One millennium (1000 years).
    /// </summary>
    Millennium
}