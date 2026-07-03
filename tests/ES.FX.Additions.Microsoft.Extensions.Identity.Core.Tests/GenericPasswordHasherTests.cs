using ES.FX.Additions.Microsoft.Extensions.Identity.Core.Passwords;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ES.FX.Additions.Microsoft.Extensions.Identity.Core.Tests;

public class GenericPasswordHasherTests
{
    private const string Password = "Correct-Horse-Battery-Staple-42";

    [Fact]
    public void HashPassword_ProducesNonEmpty_NonPlaintext_Hash()
    {
        var hasher = new GenericPasswordHasher();

        var hash = hasher.HashPassword(Password);

        Assert.False(string.IsNullOrWhiteSpace(hash));
        // The hash must not leak the original password.
        Assert.NotEqual(Password, hash);
        Assert.DoesNotContain(Password, hash);
    }

    [Fact]
    public void HashPassword_IsSalted_ProducesDifferentHashesForSameInput()
    {
        var hasher = new GenericPasswordHasher();

        var hash1 = hasher.HashPassword(Password);
        var hash2 = hasher.HashPassword(Password);

        // A salted hasher must not yield identical output for identical input.
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyHashedPassword_WithCorrectPassword_ReturnsSuccess()
    {
        var hasher = new GenericPasswordHasher();

        var hash = hasher.HashPassword(Password);
        var result = hasher.VerifyHashedPassword(hash, Password);

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyHashedPassword_WithWrongPassword_ReturnsFailed()
    {
        var hasher = new GenericPasswordHasher();

        var hash = hasher.HashPassword(Password);
        var result = hasher.VerifyHashedPassword(hash, "not-the-password");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void RoundTrip_AcrossSeparateInstances_Succeeds()
    {
        // Hashing on one instance and verifying on another must work: the hasher is stateless.
        var hasherA = new GenericPasswordHasher();
        var hasherB = new GenericPasswordHasher();

        var hash = hasherA.HashPassword(Password);

        Assert.Equal(PasswordVerificationResult.Success, hasherB.VerifyHashedPassword(hash, Password));
    }

    [Fact]
    public void Instance_IsSharedSingleton()
    {
        Assert.NotNull(GenericPasswordHasher.Instance);
        Assert.Same(GenericPasswordHasher.Instance, GenericPasswordHasher.Instance);
    }

    [Fact]
    public void Instance_RoundTrip_Succeeds()
    {
        var hash = GenericPasswordHasher.Instance.HashPassword(Password);

        Assert.Equal(PasswordVerificationResult.Success,
            GenericPasswordHasher.Instance.VerifyHashedPassword(hash, Password));
        Assert.Equal(PasswordVerificationResult.Failed,
            GenericPasswordHasher.Instance.VerifyHashedPassword(hash, "wrong"));
    }

    [Fact]
    public void OptionsConstructor_WithConfiguredIterations_StillRoundTrips()
    {
        // The IOptions<PasswordHasherOptions> overload must honor supplied options while
        // preserving hash+verify behavior.
        var options = Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = 120_000
        });

        var hasher = new GenericPasswordHasher(options);

        var hash = hasher.HashPassword(Password);

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(hash, Password));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(hash, "wrong"));
    }

    [Fact]
    public void OptionsConstructor_V2CompatibilityMode_ProducesVerifiableHash()
    {
        // Exercise a distinct, observable configuration: V2 mode emits a different hash format byte.
        var options = Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2
        });

        var hasher = new GenericPasswordHasher(options);
        var hash = hasher.HashPassword(Password);

        // IdentityV2 format marker is 0x00 -> Base64 of a payload starting with 0x00 begins with 'A'.
        var bytes = Convert.FromBase64String(hash);
        Assert.Equal(0x00, bytes[0]);

        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(hash, Password));
    }

    [Fact]
    public void DefaultHasher_UsesIdentityV3Format()
    {
        // Default (parameterless) hasher uses IdentityV3, whose format marker is 0x01.
        var hasher = new GenericPasswordHasher();
        var hash = hasher.HashPassword(Password);

        var bytes = Convert.FromBase64String(hash);
        Assert.Equal(0x01, bytes[0]);
    }

    [Fact]
    public void CrossMode_VerifyingV2HashWithDefaultV3Hasher_StillSucceeds()
    {
        // PasswordHasher can verify legacy V2 hashes even when configured for V3.
        var v2Hasher = new GenericPasswordHasher(
            Options.Create(new PasswordHasherOptions
            {
                CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2
            }));
        var v3Hasher = new GenericPasswordHasher();

        var v2Hash = v2Hasher.HashPassword(Password);

        var result = v3Hasher.VerifyHashedPassword(v2Hash, Password);

        // A correct password against a legacy-format hash verifies, and the V3 hasher signals a rehash is needed.
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);
    }
}
