using System.Text.Json.Serialization;

namespace ES.FX.OpenData.Vies.Internal;

internal sealed class ViesCheckRequest
{
    public string CountryCode { get; set; } = "";
    public string VatNumber { get; set; } = "";
}

internal sealed class ViesCheckResponse
{
    public string? CountryCode { get; set; }
    public string? VatNumber { get; set; }
    public string? RequestDate { get; set; }
    public bool? Valid { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? UserError { get; set; }
    public ViesErrorWrapper[]? ErrorWrappers { get; set; }
}

internal sealed class ViesErrorWrapper
{
    public string? Error { get; set; }
    public string? Message { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ViesCheckRequest))]
[JsonSerializable(typeof(ViesCheckResponse))]
internal partial class ViesJsonContext : JsonSerializerContext;