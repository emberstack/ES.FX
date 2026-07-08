using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ES.FX.OpenData.Vies.Internal;

internal sealed class ViesClient(IHttpClientFactory httpClientFactory, string httpClientName) : IViesClient
{
    public async Task<ViesVatValidation> ValidateAsync(string countryCode, string vatNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(vatNumber);

        using var activity = ViesClientInstrumentation.ActivitySource.StartActivity(
            "VIES ValidateVatNumber", ActivityKind.Client);
        activity?.SetTag("vies.country_code", countryCode.Trim().ToUpperInvariant());

        var http = httpClientFactory.CreateClient(httpClientName);
        var requestJson = JsonSerializer.Serialize(
            new ViesCheckRequest { CountryCode = countryCode.Trim().ToUpperInvariant(), VatNumber = vatNumber.Trim() },
            ViesJsonContext.Default.ViesCheckRequest);

        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync("check-vat-number", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new ViesApiException(response.StatusCode, OpenDataHttp.Truncate(body),
                    $"VIES request failed with status code {(int)response.StatusCode}.")
                { RetryAfter = OpenDataHttp.GetRetryAfter(response) };

        ViesCheckResponse? dto;
        try
        {
            dto = JsonSerializer.Deserialize(body, ViesJsonContext.Default.ViesCheckResponse);
        }
        catch (JsonException exception)
        {
            throw new ViesApiException(response.StatusCode, OpenDataHttp.Truncate(body),
                "VIES returned a response body that could not be parsed.", exception);
        }

        if (dto is null)
            throw new ViesApiException(response.StatusCode, OpenDataHttp.Truncate(body),
                "VIES returned an empty response body.");

        var fault = dto.UserError ?? dto.ErrorWrappers?.FirstOrDefault()?.Error;
        if (!string.IsNullOrWhiteSpace(fault) && !IsOutcomeCode(fault))
        {
            if (fault.Equals("MS_UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
                return Build(dto, ViesValidationStatus.MemberStateUnavailable, countryCode, vatNumber);

            if (fault.Equals("INVALID_INPUT", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"VIES rejected the input (country '{countryCode}', VAT number '{vatNumber}').");

            throw new ViesApiException(response.StatusCode, OpenDataHttp.Truncate(body), $"VIES service fault: {fault}.")
                { FaultCode = fault, RetryAfter = OpenDataHttp.GetRetryAfter(response) };
        }

        var status = dto.Valid == true ? ViesValidationStatus.Valid : ViesValidationStatus.Invalid;
        activity?.SetTag("vies.status", status.ToString());
        return Build(dto, status, countryCode, vatNumber);
    }

    private static ViesVatValidation Build(ViesCheckResponse dto, ViesValidationStatus status,
        string countryCode, string vatNumber) =>
        new()
        {
            Status = status,
            CountryCode = dto.CountryCode ?? countryCode.Trim().ToUpperInvariant(),
            VatNumber = dto.VatNumber ?? vatNumber.Trim(),
            RequestDate = ParseDate(dto.RequestDate),
            Name = Clean(dto.Name),
            Address = Clean(dto.Address)
        };

    // "VALID"/"INVALID" appear as normal outcome markers, not faults.
    private static bool IsOutcomeCode(string code) =>
        code.Equals("VALID", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("INVALID", StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "---" ? null : value;

    private static DateTimeOffset ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : default;
}
