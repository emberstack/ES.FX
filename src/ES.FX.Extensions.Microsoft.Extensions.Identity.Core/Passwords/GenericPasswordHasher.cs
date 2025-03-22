using JetBrains.Annotations;
using Microsoft.AspNetCore.Identity;

namespace ES.FX.Extensions.Microsoft.Extensions.Identity.Core.Passwords;

/// <summary>
///     A generic (non-user dependent) password hasher based on <see cref="IPasswordHasher{TUser}" />
/// </summary>
[PublicAPI]
public class GenericPasswordHasher
{
    private static readonly object User = new();
    private readonly PasswordHasher<object> _hasher = new();

    public static GenericPasswordHasher Instance { get; } = new();

    /// <summary>
    ///     Returns a hashed representation of the supplied <paramref name="password" />.
    /// </summary>
    public string HashPassword(string password) => _hasher.HashPassword(User, password);

    /// <summary>
    ///     Returns a <see cref="PasswordVerificationResult" /> indicating the result of a password hash comparison.
    /// </summary>
    /// <param name="hashedPassword">The hash value for the password.</param>
    /// <param name="providedPassword">The password supplied for comparison.</param>
    /// <returns>A <see cref="PasswordVerificationResult" /> indicating the result of a password hash comparison.</returns>
    /// <remarks>Implementations of this method should be time consistent.</remarks>
    public PasswordVerificationResult VerifyHashedPassword(string hashedPassword, string providedPassword) =>
        _hasher.VerifyHashedPassword(User, hashedPassword, providedPassword);
}