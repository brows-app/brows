using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public sealed class SecurityCredential {
        public string Username { get; set; }
        public string Password { get; private set; }
        public SecurityDataKind PasswordKind { get; private set; }

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

        public async Task ProtectPassword(string pass, SecurityDataKind kind, CancellationToken token) {
            if (token.IsCancellationRequested) {
                await Task.FromCanceled(token).ConfigureAwait(false);
            }
            PasswordKind = kind;
            Password = kind switch {
                SecurityDataKind.PlainText => pass,
                SecurityDataKind.Protected => string.IsNullOrEmpty(pass)
                    ? pass
                    : await SecurityData.Protect(pass, token).ConfigureAwait(false),
                _ => throw new InvalidOperationException(message: $"Invalid password kind: '{kind}'"),
            };
        }

        public async Task ProtectPassword(SecureString value, SecurityDataKind kind, CancellationToken token) {
            var p = Marshal.SecureStringToGlobalAllocAnsi(value);
            try {
                await ProtectPassword(Marshal.PtrToStringAnsi(p), kind, token).ConfigureAwait(false);
            }
            finally {
                Marshal.ZeroFreeGlobalAllocAnsi(p);
            }
        }
    }
}
