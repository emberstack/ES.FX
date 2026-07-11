using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ES.FX.OpenData.Romania.Anaf.VatCheck.Internal;

/// <summary>
///     The embedded ANAF (MFP) locality-code → SIRUTA crosswalk. Resolves an ANAF address's
///     <c>(cod_Judet, cod_Localitate)</c> pair to the SIRUTA code of the territorial unit it points at.
///     Extracted verbatim from the Ministry of Finance geographic nomenclature (edition 2026-07); the
///     synthetic "Fără domiciliu" pseudo-localities are excluded. Loaded once, lazily, into a frozen map.
/// </summary>
internal sealed class AnafSirutaCrosswalk
{
    private const string ResourceName = "ES.FX.OpenData.Romania.Anaf.anaf-siruta.2026-07.json";

    private readonly Lazy<FrozenDictionary<(string CountyCode, string LocalityCode), int>> _map;

    public AnafSirutaCrosswalk() =>
        _map = new Lazy<FrozenDictionary<(string, string), int>>(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     Resolves an ANAF <c>(cod_Judet, cod_Localitate)</c> pair to a SIRUTA code, or <c>null</c> when either
    ///     code is blank or the pair is not in the crosswalk.
    /// </summary>
    public int? Find(string? countyCode, string? localityCode)
    {
        if (string.IsNullOrWhiteSpace(countyCode) || string.IsNullOrWhiteSpace(localityCode)) return null;
        return _map.Value.TryGetValue((countyCode, localityCode), out var siruta) ? siruta : null;
    }

    private static FrozenDictionary<(string, string), int> Load()
    {
        var assembly = typeof(AnafSirutaCrosswalk).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"Embedded resource '{ResourceName}' was not found in assembly '{assembly.GetName().Name}'.");

        var typeInfo = (JsonTypeInfo<Dictionary<string, Dictionary<string, int>>>)
            AnafSirutaCrosswalkJsonContext.Default.GetTypeInfo(typeof(Dictionary<string, Dictionary<string, int>>))!;
        var nested = JsonSerializer.Deserialize(stream, typeInfo)
                     ?? throw new InvalidOperationException("The ANAF→SIRUTA crosswalk resource could not be parsed.");

        var builder = new Dictionary<(string, string), int>(20_000);
        foreach (var (countyCode, localities) in nested)
        foreach (var (localityCode, siruta) in localities)
            builder[(countyCode, localityCode)] = siruta;
        return builder.ToFrozenDictionary();
    }
}

[JsonSerializable(typeof(Dictionary<string, Dictionary<string, int>>))]
internal partial class AnafSirutaCrosswalkJsonContext : JsonSerializerContext;