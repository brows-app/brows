using Brows.Connection;
using Brows.Panels.PanelSecrets;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SftpConnectionProvider : Provider<SftpConnectionEntry, SftpConnectionConfig> {
    private static readonly ILog Log = Logging.For(typeof(SftpConnectionProvider));

    private readonly SftpProviderFactory Factory;
    private readonly SftpClientLease Lease;
    private int EntryOrder;

    private SftpClient Client => Lease.Client;

    private string[] PrivateKeyFiles() {
        var keyOptions = Config.PrivateKey;
        var host = Client.HostName;
        var option = keyOptions.TryGetValue(host, out var current)
            ? current
            : keyOptions.TryGetValue("*", out current)
                ? current
                : null;
        if (option is null) {
            return [];
        }
        var home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolderOption.DoNotVerify);
        var result = new List<string>();
        foreach (var configured in option.File) {
            if (string.IsNullOrWhiteSpace(configured)) {
                continue;
            }
            var configuredUsesHomePrefix = configured.StartsWith("~/", StringComparison.Ordinal)
                || configured.StartsWith("~\\", StringComparison.Ordinal);
            var path = configuredUsesHomePrefix
                ? Path.Combine(home, configured[2..])
                : configured;
            var privateKeyFile = Path.GetFullPath(path);
            if (File.Exists(privateKeyFile)) {
                result.Add(privateKeyFile);
            }
        }
        return result.ToArray();
    }

    private async Task<bool> TryPrivateKeys(IReadOnlyList<string> privateKeyFiles,
                                            SecureString passphrase,
                                            CancellationToken token) {
        foreach (var privateKeyFile in privateKeyFiles) {
            if (Log.Info()) {
                Log.Info(Log.Join("Trying SFTP private key", privateKeyFile, "passphrase?", passphrase is not null));
            }
            if (await Client.AuthenticateByKey(publicKeyFile: null, privateKeyFile, passphrase, token)) {
                return true;
            }
        }
        return false;
    }

    private async Task<SecureString> GetSecretPassword(string key, string promptFormat, string[] promptArgs, CancellationToken token) {
        var secret = await GetSecret<ISecretPassword>(key, promptFormat, promptArgs, token);
        return secret?.Password;
    }

    private async Task Authenticate(CancellationToken token) {
        if (!Client.HostApproved) {
            return;
        }

        var privateKeyFiles = PrivateKeyFiles();
        var result = await SftpAuthenticationCoordinator.Authenticate(
            Client.HostName,
            privateKeyFiles,
            cancellationToken => TryPrivateKeys(privateKeyFiles, passphrase: null, cancellationToken),
            (host, cancellationToken) => GetSecretPassword($"sftp://{host}", "SftpConnection_Prompt_Password", [host], cancellationToken),
            Client.AuthenticateByPassword,
            (host, keyFile, cancellationToken) => GetSecretPassword(
                $"sftp://{host}|{keyFile}",
                "SftpConnection_Prompt_PrivateKeyPassphrase",
                [keyFile, host],
                cancellationToken),
            (keyFile, passphrase, cancellationToken) => Client.AuthenticateByKey(
                publicKeyFile: null,
                keyFile,
                passphrase,
                cancellationToken),
            token);
        if (result == SftpAuthenticationResult.Authenticated) {
            await Change(ID, token);
            return;
        }
        if (result == SftpAuthenticationResult.Canceled) {
            await Provide(new SftpConnectionEntry(
                this,
                EntryOrder++,
                "Authentication canceled; open to try again",
                action: RetryAuthentication), token);
            return;
        }

        await Provide(new SftpConnectionEntry(
            this,
            EntryOrder++,
            "Authentication failed; open to retry",
            action: RetryAuthentication), token);
    }

    private async Task<bool> RetryAuthentication(CancellationToken token) {
        await Revoke(Provided, token);
        await Authenticate(token);
        return true;
    }

    private async Task<bool> AuthenticationCanceled(CancellationToken token) {
        await Revoke(Provided, token);
        await Authenticate(token);
        return true;
    }

    private async Task<bool> AcceptHostKey(CancellationToken token) {
        var accepted = await Client.TrustHostKey(token);
        if (!accepted) {
            return false;
        }
        await Revoke(Provided, token);
        await Authenticate(token);
        return true;
    }

    protected sealed override async Task Begin(CancellationToken token) {
        await Client.Connect(token);
        if (Client.KnownHostState is SftpKnownHostStatus.NotFound or SftpKnownHostStatus.Mismatch) {
            await Provide(new SftpConnectionEntry(this,
                                                 EntryOrder++,
                                                 "Host key fingerprint",
                                                 Client.Fingerprint), token);
            var action = Client.KnownHostState == SftpKnownHostStatus.Mismatch
                ? "Replace changed host key after checking the fingerprint"
                : "Trust first host key after checking the fingerprint";
            await Provide(new SftpConnectionEntry(this,
                                                 EntryOrder++,
                                                 action,
                                                 Client.KnownHostState,
                                                 AcceptHostKey), token);
            return;
        }
        if (Client.KnownHostState == SftpKnownHostStatus.Match) {
            await Authenticate(token);
            return;
        }
        throw new IOException($"The SFTP host key could not be checked for {Client.HostName}:{Client.Port}.");
    }

    protected sealed override void End() {
        Lease.Dispose();
        base.End();
    }

    internal object Icon { get; }

    internal SftpConnectionProvider(SftpProviderFactory factory, Uri uri, object icon, SftpClientLease lease)
        : base(uri?.ToString()) {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        Icon = icon;
        Lease = lease ?? throw new ArgumentNullException(nameof(lease));
    }
}
