using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.SSH {
    internal sealed class SSHClientCache {
        private static readonly ILog Log = Logging.For(typeof(SSHClientCache));

        private readonly Dictionary<BigInteger, SSHClient> Agent = new();

        private static Task<BigInteger> ComputeKey(Uri uri, CancellationToken token) {
            return Task.Run(cancellationToken: token, function: () => {
                var keyUri = new UriBuilder(uri) { Fragment = null, Path = "/", Query = null }.Uri;
                var keyUriBytes = Encoding.UTF8.GetBytes($"{keyUri}");
                var hashBytes = keyUriBytes;
                var hash = default(byte[]);
                using (var hashAlgo = SHA256.Create()) {
                    hash = hashAlgo.ComputeHash(hashBytes);
                }
                return new BigInteger(hash);
            });
        }

        private void SSHClient_Disposing(object sender, EventArgs e) {
            var keys = Agent.Keys.Where(key => Agent[key] == sender).ToList();
            foreach (var key in keys) {
                if (Log.Info()) {
                    Log.Info($"{nameof(Agent.Remove)} > {Agent[key].Uri}");
                }
                Agent[key].Disposing -= SSHClient_Disposing;
                Agent.Remove(key);
            }
        }

        public async Task<SSHClient> GetOrAdd(Uri uri, CancellationToken token) {
            var key = await ComputeKey(uri, token).ConfigureAwait(false);
            if (Agent.TryGetValue(key, out var value) == false) {
                lock (Agent) {
                    if (Agent.TryGetValue(key, out value) == false) {
                        if (Log.Info()) {
                            Log.Info($"{nameof(SSHClient.Create)} > {uri}");
                        }
                        Agent[key] = value = SSHClient.Create(uri);
                        Agent[key].Disposing += SSHClient_Disposing;
                    }
                }
            }
            return value;
        }
    }
}
