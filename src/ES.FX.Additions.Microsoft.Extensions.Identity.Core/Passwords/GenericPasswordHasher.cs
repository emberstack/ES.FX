using JetBrains.Annotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ES.FX.Additions.Microsoft.Extensions.Identity.Core.Passwords;

/// <summary>
///     A generic (non-user dependent) password hasher based on <see cref="IPasswordHasher{TUser}" />
/// </summary>
[PublicAPI]
public class GenericPasswordHasher
{
    private static readonly object User = new();
    private readonly PasswordHasher<object> _hasher;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GenericPasswordHasher" /> class using the default
    ///     <see cref="PasswordHasherOptions" />.
    /// </summary>
    public GenericPasswordHasher() => _hasher = new PasswordHasher<object>();

    /// <summary>
    ///     Initializes a new instance of the <see cref="GenericPasswordHasher" /> class using the supplied
    ///     <paramref name="optionsAccessor" />.
    /// </summary>
    /// <param name="optionsAccessor">The options used to configure the underlying <see cref="PasswordHasher{TUser}" />.</param>
    public GenericPasswordHasher(IOptions<PasswordHasherOptions> optionsAccessor) =>
        _hasher = new PasswordHasher<object>(optionsAccessor);

    /// <summary>
    ///     Gets a shared default instance of the hasher.
    /// </summary>
    public static GenericPasswordHasher Instance { get; } = new();

    /// <summary>
    ///     Returns a hashed representation of the supplied <paramref name="password" />.
    /// </summary>
    /// <param name="password">The password to hash.</param>
    /// <returns>A hashed representation of the supplied <paramref name="password" />.</returns>
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