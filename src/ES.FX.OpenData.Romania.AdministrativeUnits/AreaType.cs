using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.AdministrativeUnits;

/// <summary>SIRUTA area (mediu) classification of a UAT or locality.</summary>
[PublicAPI]
public enum AreaType
{
    /// <summary>
    ///     Not applicable — only counties carry no area classification. UAT-level units and localities are
    ///     classified <see cref="Urban" /> or <see cref="Rural" />.
    /// </summary>
    None = 0,

    /// <summary>Urban (<c>urban</c>).</summary>
    Urban = 1,

    /// <summary>Rural (<c>rural</c>).</summary>
    Rural = 3
}
