using JetBrains.Annotations;

namespace ES.FX.Collections;

[PublicAPI]
public static class ArrayExtensions
{
    /// <summary>
    /// Checks if the array is null or empty
    /// </summary>
    public static bool IsNullOrEmpty(this Array? array)
    {
        return array is null || array.Length == 0;
    }
}