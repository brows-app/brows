using Brows.SSH.Clients;
using Brows.SSH.Native;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.SSH {
    internal abstract class SSHClient : SSHClientBase, IAsyncDisposable {
        private static readonly ILog Log = Logging.For(typeof(SSHClient));

        private int ProviderCount;
        private SSHClientSession Session;

        protected async IAsyncEnumerable<SSHClientOutput> SSH(string command, [EnumeratorCancellation] CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(SSH), command));
            }
            var stdOutBuilder = new SSHServerResponseLineBuilder();
            var stdErrBuilder = new SSHServerResponseLineBuilder();
            var clientCommand = new SSHClientCommand {
                Command = command,
                StdErrBuilder = stdErrBuilder,
                StdOutBuilder = stdOutBuilder
            };
            await Session.Execute(clientCommand, token).ConfigureAwait(false);

            await using var stdout = stdOutBuilder.Lines(token).GetAsyncEnumerator(token);
            await using var stderr = stdErrBuilder.Lines(token).GetAsyncEnumerator(token);

            var moveOut = stdout.MoveNextAsync().AsTask();
            var moveErr = stderr.MoveNextAsync().AsTask();

            for (; ; ) {
                var moveComplete =
                    moveOut != null && moveErr != null ? await Task.WhenAny(moveOut, moveErr).ConfigureAwait(false) :
                    moveOut != null ? moveOut :
                    moveErr != null ? moveErr :
                    null;
                if (moveComplete == null) {
                    break;
                }
                if (moveComplete == moveOut) {
                    var moved = await moveOut.ConfigureAwait(false);
                    if (moved) {
                        if (Log.Info()) {
                            Log.Info(Log.Join(nameof(stdout), stdout.Current));
                        }
                        yield return new SSHClientOutput(stdout.Current, SSHClientOutputKind.StdOut);
                        moveOut = stdout.MoveNextAsync().AsTask();
                    }
                    else {
                        moveOut = null;
                    }
                }
                if (moveComplete == moveErr) {
                    var moved = await moveErr.ConfigureAwait(false);
                    if (moved) {
                        if (Log.Info()) {
                            Log.Info(Log.Join(nameof(stderr), stderr.Current));
                        }
                        yield return new SSHClientOutput(stderr.Current, SSHClientOutputKind.StdErr);
                        moveErr = stderr.MoveNextAsync().AsTask();
                    }
                    else {
                        moveErr = null;
                    }
                }
            }
        }

        public event EventHandler Disposing;

        public SSHKnownHost KnownHost =>
            Session.KnownHost;

        public SSHFingerprint HostFingerprint =>
            Session.HostFingerprint;

        public Uri Uri =>
            Session.Uri;

        public int Port =>
            Session.Port;

        public string HostName =>
            Session.HostName;

        public IPAddress HostAddress =>
            Session.HostAddress;

        public bool Connected =>
            Session.Connected;

        public abstract IAsyncEnumerable<SSHFileInfo> List(string path, CancellationToken token);

        public async IAsyncEnumerable<SSHFileInfo> List(Uri uri, [EnumeratorCancellation] CancellationToken token) {
            ArgumentNullException.ThrowIfNull(uri);
            var path = Uri.UnescapeDataString(uri.AbsolutePath);
            var list = List(path, token);
            await foreach (var info in list.ConfigureAwait(false)) {
                yield return info;
            }
        }

        public async IAsyncEnumerable<SSHFileInfo> ListRecursively(Uri uri, [EnumeratorCancellation] CancellationToken token) {
            var list = List(uri, token);
            await foreach (var item in list.ConfigureAwait(false)) {
                switch (item.Kind) {
                    case SSHEntryKind.File:
                        yield return item;
                        break;
                    case SSHEntryKind.Directory:
                        yield return item;
                        var
                        urib = new UriBuilder(uri);
                        urib.Path = item.Path;
                        await foreach (var i in ListRecursively(urib.Uri, token).ConfigureAwait(false)) {
                            yield return i;
                        }
                        break;
                }
            }
        }

        public static SSHClient Create(Uri uri) {
            var client = new PosixSSHClient();
            var session = new SSHClientSession(uri);
            client.Session = session;
            return client;
        }

        public int Providing() {
            return ++ProviderCount;
        }

        public int Released() {
            return --ProviderCount;
        }

        public Task<ScpRecv> SCPRecv(string path, CancellationToken token) {
            return Session.SCPRecv(path, token);
        }

        public Task<ScpSend> SCPSend(string path, int mode, long size, CancellationToken token) {
            return Session.SCPSend(path, mode, size, token);
        }

        public Task Authenticate(SSHAuth auth, CancellationToken token) {
            return Session.Authenticate(auth, token);
        }

        public Task<bool> Authenticated(CancellationToken token) {
            return Session.Authenticated(token);
        }

        public Task<object> Connection(CancellationToken token) {
            return Session.Connection(token);
        }

        async ValueTask IAsyncDisposable.DisposeAsync() {
            Disposing?.Invoke(this, EventArgs.Empty);
            await using (Session) {
                Session = null;
            }
        }
    }
}
