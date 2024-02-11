using Brows.SSH;
using Brows.SSH.Exceptions;
using Domore.IO;
using Domore.Logs;
using System;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using PATH = System.IO.Path;

namespace Brows.SSHConnection {
    internal sealed class SSHConnectionProvider : SSHProviderBase<SSHConnectionEntry, SSHConnectionConfig> {
        private static readonly ILog Log = Logging.For(typeof(SSHConnectionProvider));

        private int EntryOrder;

        private async Task<bool> AuthByPrivateKey(SecureString passphrase, CancellationToken token) {
            var host = Uri.Host;
            var privateKey = Config.PrivateKey;
            var privateKeyOpt =
                privateKey.TryGetValue(host, out var opt) ? opt :
                privateKey.TryGetValue("*", out opt) ? opt :
                null;
            if (privateKeyOpt == null) {
                if (Log.Info()) {
                    Log.Info(Log.Join("No private key options found for host", host));
                }
                return false;
            }
            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
            var client = await Client.Ready(token);
            var privateKeyFiles = privateKeyOpt.File
                .Select(path => path.StartsWith("~/") || path.StartsWith("~\\")
                    ? string.Concat(user, path.AsSpan(1))
                    : path)
                .Select(path => path.Split([PATH.DirectorySeparatorChar, PATH.AltDirectorySeparatorChar]))
                .Select(PATH.Join);
            foreach (var keyPath in privateKeyFiles) {
                var file = await FileSystemTask.ExistingFile(keyPath, token);
                if (file != null) {
                    if (Log.Info()) {
                        Log.Info(Log.Join("Using private key", keyPath, "with passphrase?", passphrase is null ? "no" : "yes"));
                    }
                    try {
                        await client.Authenticate(token: token, auth: new SSHAuth {
                            PrivateKeyFile = file.FullName,
                            Secret = passphrase
                        });
                        if (Log.Info()) {
                            Log.Info(Log.Join("Private key", keyPath, "with passphrase?", passphrase is null ? "no" : "yes", "authenticated"));
                        }
                        return true;
                    }
                    catch (Exception ex) {
                        if (Log.Info()) {
                            Log.Info(Log.Join("Private key failed", keyPath, "with passphrase?", passphrase is null ? "no" : "yes", ex?.GetType()?.Name, ex is BadCredentialException bce 
                                ? bce.Name 
                                : ex?.Message));
                        }
                        if (Log.Debug()) {
                            Log.Debug(ex);
                        }
                    }
                }
                else {
                    if (Log.Info()) {
                        Log.Info(Log.Join("Private key", keyPath, "does not exist"));
                    }
                }
            }
            return false;
        }

        private async Task<bool> AuthByPassword(SecureString password, CancellationToken token) {
            if (Log.Info()) {
                Log.Info("Using password");
            }
            var client = await Client.Ready(token);
            try {
                await client.Authenticate(token: token, auth: new SSHAuth {
                    Secret = password
                });
                if (Log.Info()) {
                    Log.Info("Password authenticated");
                }
                return true;
            }
            catch (Exception ex) {
                if (Log.Info()) {
                    Log.Info(Log.Join("Password failed", ex?.GetType()?.Name, ex is BadCredentialException bce ? bce.Name : ex?.Message));
                }
                if (Log.Debug()) {
                    Log.Debug(ex);
                }
                return false;
            }
        }

        private async Task<bool?> Auth(CancellationToken token) {
            var authByPrivateKey = await AuthByPrivateKey(passphrase: null, token);
            if (authByPrivateKey) {
                return true;
            }
            var secret = await GetSecret(promptFormat: "SSHConnection_Prompt_Password", promptArgs: new[] { Uri.Host }, token: token);
            if (secret == null) {
                return null;
            }
            var authByPassword = await AuthByPassword(secret, token);
            if (authByPassword) {
                return true;
            }
            var authByPrivateKeyPassphrase = await AuthByPrivateKey(passphrase: secret, token);
            if (authByPrivateKeyPassphrase) {
                return true;
            }
            return false;
        }

        private async Task Authenticate(CancellationToken token) {
            var auth = await Auth(token);
            if (auth == null) {
                await Provide(new SSHConnectionEntry(this, order: EntryOrder++, kind: nameof(UserCanceled), data: null, task: UserCanceled));
                return;
            }
            if (auth == false) {
                await Provide(new SSHConnectionEntry(this, order: EntryOrder++, kind: nameof(BadCredential), data: null, task: BadCredential));
                return;
            }
            await Task.Delay(500, token);
            await Change($"{Uri}", token);
        }

        private async Task<bool> UserCanceled(CancellationToken token) {
            await Revoke(Provided);
            await Authenticate(token);
            return true;
        }

        private async Task<bool> AuthenticateError(CancellationToken token) {
            await Task.CompletedTask;
            return true;
        }

        private async Task<bool> BadCredential(CancellationToken token) {
            await Revoke(Provided);
            await Authenticate(token);
            return true;
        }

        private async Task<bool> AcceptFingerprintTemporarily(CancellationToken token) {
            await Revoke(Provided);
            await Authenticate(token);
            return true;
        }

        private async Task<bool> AcceptFingerprintPermanently(CancellationToken token) {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        protected sealed override async Task Begin(CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(Begin), ID));
            }
            var client = await Client.Ready(token);
            var connected = await client.Connection(token);
            var knownHost = client.KnownHost;
            if (knownHost == null) {
                throw new InvalidOperationException($"No known host");
            }
            switch (knownHost.Status) {
                case SSHKnownHostStatus.Match:
                    await Authenticate(token);
                    break;
                case SSHKnownHostStatus.Mismatch:
                    goto case SSHKnownHostStatus.NotFound;
                case SSHKnownHostStatus.NotFound:
                    await Provide(new[] {
                        new SSHConnectionEntry(this, order: EntryOrder++, kind: nameof(client.HostFingerprint), data: client, task: null),
                        //new SSHConnectionEntry(this, order: EntryOrder++, kind: nameof(AcceptFingerprintPermanently), data: null, task: AcceptFingerprintPermanently),
                        new SSHConnectionEntry(this, order: EntryOrder++, kind: nameof(AcceptFingerprintTemporarily), data: null, task: AcceptFingerprintTemporarily)
                    });
                    return;
                default:
                    throw new InvalidOperationException($"Not implemented [{nameof(SSHKnownHostStatus)}.{knownHost.Status}]");
            }
        }

        public SSHConnectionProvider(SSHProviderFactory factory, Uri uri, object icon) : base(factory, uri, icon) {
        }
    }
}
