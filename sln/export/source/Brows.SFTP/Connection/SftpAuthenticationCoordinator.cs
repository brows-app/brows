using System;
using System.Collections.Generic;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Connection;

internal static class SftpAuthenticationCoordinator {
    internal static async Task<SftpAuthenticationResult> Authenticate(
        string host,
        IReadOnlyList<string> privateKeyFiles,
        Func<CancellationToken, Task<bool>> tryUnencryptedKeys,
        Func<string, CancellationToken, Task<SecureString>> promptPassword,
        Func<SecureString, CancellationToken, Task<bool>> authenticateByPassword,
        Func<string, string, CancellationToken, Task<SecureString>> promptPrivateKeyPassphrase,
        Func<string, SecureString, CancellationToken, Task<bool>> authenticateByKey,
        CancellationToken token) {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(privateKeyFiles);
        ArgumentNullException.ThrowIfNull(tryUnencryptedKeys);
        ArgumentNullException.ThrowIfNull(promptPassword);
        ArgumentNullException.ThrowIfNull(authenticateByPassword);
        ArgumentNullException.ThrowIfNull(promptPrivateKeyPassphrase);
        ArgumentNullException.ThrowIfNull(authenticateByKey);

        if (await tryUnencryptedKeys(token)) {
            return SftpAuthenticationResult.Authenticated;
        }
        token.ThrowIfCancellationRequested();

        foreach (var privateKeyFile in privateKeyFiles) {
            token.ThrowIfCancellationRequested();
            var passphrase = await promptPrivateKeyPassphrase(host, privateKeyFile, token);
            if (passphrase is null) {
                return SftpAuthenticationResult.Canceled;
            }
            try {
                token.ThrowIfCancellationRequested();
                if (await authenticateByKey(privateKeyFile, passphrase, token)) {
                    return SftpAuthenticationResult.Authenticated;
                }
            }
            finally {
                passphrase.Dispose();
            }
        }
        token.ThrowIfCancellationRequested();

        var password = await promptPassword(host, token);
        if (password is null) {
            return SftpAuthenticationResult.Canceled;
        }
        bool passwordAuthenticated;
        try {
            token.ThrowIfCancellationRequested();
            passwordAuthenticated = await authenticateByPassword(password, token);
        }
        finally {
            password.Dispose();
        }
        if (passwordAuthenticated) {
            return SftpAuthenticationResult.Authenticated;
        }
        token.ThrowIfCancellationRequested();
        return SftpAuthenticationResult.Failed;
    }
}
