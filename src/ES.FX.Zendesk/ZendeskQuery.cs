using System.Globalization;

namespace ES.FX.Zendesk;

/// <summary>
///     Helpers for building relative request URIs with query strings (skips null/empty values, URL-encodes values).
/// </summary>
internal static class ZendeskQuery
{
    public static string Build(string path, params (string Key, string? Value)[] parameters)
    {
        var parts = new List<string>(parameters.Length);
        foreach (var (key, value) in parameters)
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add($"{key}={Uri.EscapeDataString(value)}");

        return parts.Count == 0 ? path : $"{path}?{string.Join('&', parts)}";
    }

    public static string? Int(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    public static string? Bool(bool? value) => value switch
    {
        true => "true",
        false => "false",
        null => null
    };

    /// <summary>
    ///     Builds the flat sideload value for list/show endpoints (e.g. <c>users,groups,organizations</c>), or
    ///     <c>null</c> when nothing is requested.
    /// </summary>
    public static string? Include(IReadOnlyList<string>? include) =>
        include is null || include.Count == 0 ? null : string.Join(',', include);
}