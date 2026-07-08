using System.Collections.Frozen;

namespace ES.FX.OpenData.Romania.AdministrativeUnits.Internal;

/// <summary>
///     Maps the SIRUTA county number (<c>JUD</c>) to the county's ISO 3166-2:RO abbreviation. Note the
///     historical numbering: Călărași = 51 and Giurgiu = 52 (added later), Bucharest = 40.
/// </summary>
internal static class SirutaCounties
{
    public static readonly FrozenDictionary<int, string> ByJud = new Dictionary<int, string>
    {
        [1] = "AB", [2] = "AR", [3] = "AG", [4] = "BC", [5] = "BH", [6] = "BN", [7] = "BT", [8] = "BV",
        [9] = "BR", [10] = "BZ", [11] = "CS", [51] = "CL", [12] = "CJ", [13] = "CT", [14] = "CV", [15] = "DB",
        [16] = "DJ", [17] = "GL", [52] = "GR", [18] = "GJ", [19] = "HR", [20] = "HD", [21] = "IL", [22] = "IS",
        [23] = "IF", [24] = "MM", [25] = "MH", [26] = "MS", [27] = "NT", [28] = "OT", [29] = "PH", [30] = "SM",
        [31] = "SJ", [32] = "SB", [33] = "SV", [34] = "TR", [35] = "TM", [36] = "TL", [37] = "VS", [38] = "VL",
        [39] = "VN", [40] = "B"
    }.ToFrozenDictionary();
}
