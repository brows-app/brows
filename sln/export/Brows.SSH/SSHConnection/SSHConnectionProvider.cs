using Brows.SSH;
using Brows.SSH.Exceptions;
using Domore.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.SSHConnection {
    internal sealed class SSHConnectionProvider : SSHProviderBase<SSHConnectionEntry, SSHConnectionConfig> {
        private static readonly ILog Log = Logging.For(typeof(SSHConnectionProvider));

        private int EntryOrder;

        private async Task Authenticate(CancellationToken token) {
            var client = await Client.Ready(token);
            var secret = await GetSecret(promptFormat: "SSHConnection_Prompt_Password", promptArgs: new[] { client.HostName }, token: token);
            if (secret == null) {
                await Provide(new SSHConnectionEntry(this, order: EntryOrder++, kind: nameof(UserCanceled), data: null, task: UserCanceled));
                return;
            }
            try {
                await client.Authenticate(token: token, auth: new SSHAuth {
                    Secret = secret
                });
            }
            catch (Exception ex) {
                if (ex is BadCredentialException) {
                    await Provide(new SSHConnectionEntry(this, order: EntryOrder++, kind: nameof(BadCredential), data: ex, task: BadCredential));
                }
                else {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                    await Provide(new SSHConnectionEntry(this, order: EntryOrder++, kind: nameof(AuthenticateError), data: ex, task: AuthenticateError));
                }
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
