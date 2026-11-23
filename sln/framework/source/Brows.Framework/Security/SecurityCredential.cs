using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;

namespace Brows.Security;

/// <summary>
/// Stores a username and a password in plain or protected form.
/// </summary>
public sealed class SecurityCredential {
    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets the stored password in the form specified by <see cref="PasswordKind"/>.
    /// </summary>
    public string Password { get; private set; }

    /// <summary>
    /// Gets the storage form of <see cref="Password"/>.
    /// </summary>
    public SecurityDataKind PasswordKind { get; private set; }

    /// <summary>
    /// Retrieves the password as plain text, restoring it from protected form when needed.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The plain-text password.
    /// </returns>
    public Task<string> UnprotectPassword(CancellationToken token) {
        var pass = Password;
        var kind = PasswordKind;
        return token.IsCancellationRequested
            ? Task.FromCanceled<string>(token)
            : kind switch {
                SecurityDataKind.PlainText => Task.FromResult(pass),
                SecurityDataKind.Protected => string.IsNullOrWhiteSpace(pass)
                    ? Task.FromResult(pass)
                    : SecurityData.Unprotect(pass, token),
                _ => throw new InvalidOperationException(message: $"Invalid password kind: '{kind}'")
            };
    }

    /// <summary>
    /// Stores a password in the requested form.
    /// </summary>
    /// <param name="pass">
    /// The plain-text password.
    /// </param>
    /// <param name="kind">
    /// Whether to store plain text or protected text.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the password is stored.
    /// </returns>
    public async Task ProtectPassword(string pass, SecurityDataKind kind, CancellationToken token) {
        if (token.IsCancellationRequested) {
            await Task.FromCanceled(token);
        }
        PasswordKind = kind;
        Password = kind switch {
            SecurityDataKind.PlainText => pass,
            SecurityDataKind.Protected => string.IsNullOrEmpty(pass)
                ? pass
                : await SecurityData.Protect(pass, token),
            _ => throw new InvalidOperationException(message: $"Invalid password kind: '{kind}'"),
        };
    }

    /// <summary>
    /// Stores a password in the requested form.
    /// </summary>
    /// <param name="value">
    /// The password to store.
    /// </param>
    /// <param name="kind">
    /// Whether to store plain text or protected text.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the password is stored.
    /// </returns>
    public async Task ProtectPassword(SecureString value, SecurityDataKind kind, CancellationToken token) {
        var p = Marshal.SecureStringToGlobalAllocAnsi(value);
        try {
            await ProtectPassword(Marshal.PtrToStringAnsi(p), kind, token);
        }
        finally {
            Marshal.ZeroFreeGlobalAllocAnsi(p);
        }
    }
}
