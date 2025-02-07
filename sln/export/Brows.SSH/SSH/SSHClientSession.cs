using Brows.SSH.Native;
using Domore.IO.Extensions;
using Domore.Logs;
using Domore.Notification;
using Domore.Threading.Tasks;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.SSH {
    internal sealed class SSHClientSession : Notifier, IAsyncDisposable {
        private static readonly ILog Log = Logging.For(typeof(SSHClientSession));

        private readonly SemaphoreSlim Locker = new(1, 1);

        private TaskCache<Conn>.WithRefresh Conn => _Conn ??= new TaskCache<Conn>.WithRefresh(Connect);
        private TaskCache<Conn>.WithRefresh _Conn;

        private async Task Lock(CancellationToken token) {
            if (Disposed) {
                throw new ObjectDisposedException(objectName: nameof(SSHClientSession));
            }
            await Locker.WaitAsync(token);
        }

        private void Unlock() {
            if (Disposed == false) {
                Locker.Release();
            }
        }

        private async Task<T> Lock<T>(Func<CancellationToken, Task<T>> task, CancellationToken token) {
            await Lock(token);
            try {
                var result = task?.Invoke(token);
                if (result == null) {
                    return default;
                }
                return await result;
            }
            finally {
                Unlock();
            }
        }

        private async Task<(long, int)> Host(CancellationToken token) {
            var hostType = Uri.HostNameType;
            var host = Uri.Host;
            switch (hostType) {
                case UriHostNameType.Dns:
                    var addresses = await Dns.GetHostAddressesAsync(host, token).ConfigureAwait(false);
                    var v4Address = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);
                    if (v4Address != null) {
                        HostAddress = v4Address.MapToIPv4();
                        HostFamily = AddressFamily.InterNetwork;
                        break;
                    }
                    var v6Address = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetworkV6);
                    if (v6Address != null) {
                        HostAddress = v6Address.MapToIPv6();
                        HostFamily = AddressFamily.InterNetworkV6;
                        break;
                    }
                    break;
                case UriHostNameType.IPv4:
                    HostAddress = IPAddress.Parse(host);
                    HostFamily = AddressFamily.InterNetwork;
                    break;
                case UriHostNameType.IPv6:
                    HostAddress = IPAddress.Parse(host);
                    HostFamily = AddressFamily.InterNetworkV6;
                    break;
            }
            if (HostAddress == null) {
                throw new InvalidOperationException();
            }
            var bytes = HostAddress.GetAddressBytes();
            var bits =
                HostFamily == AddressFamily.InterNetworkV6 ? BitConverter.ToInt64(bytes) :
                HostFamily == AddressFamily.InterNetwork ? BitConverter.ToInt32(bytes) :
                throw new InvalidOperationException();
            return (bits, (int)HostFamily);
        }

        private async Task<Conn> Connect(CancellationToken token) {
            Connected = false;
            Connecting = true;
            try {
                var (host, family) = await Host(token).ConfigureAwait(false);
                return await Task
                    .Run(cancellationToken: token, function: () => {
                        var conn = new Conn {
                            Host = host,
                            HostFamily = family,
                            Port = Uri.Port,
                            Username = Uri.UserInfo
                        };
                        conn.Connect();
                        HostFingerprint = new SSHFingerprint(conn.FingerprintHashFunc, conn.Fingerprint);
                        KnownHost = SSHKnownHost.Get(conn, HostName, Port);
                        Connected = true;
                        return conn;
                    })
                    .ConfigureAwait(false);
            }
            finally {
                Connecting = false;
            }
        }

        public AddressFamily HostFamily {
            get => _HostFamily;
            private set => Change(ref _HostFamily, value, nameof(HostFamily));
        }
        private AddressFamily _HostFamily;

        public IPAddress HostAddress {
            get => _HostAddress;
            private set => Change(ref _HostAddress, value, nameof(HostAddress));
        }
        private IPAddress _HostAddress;

        public string HostName =>
            Uri.Host;

        public int Port =>
            Uri.Port < 0 ? 22 : Uri.Port;

        public bool Connecting {
            get => _Connecting;
            private set => Change(ref _Connecting, value, nameof(Connecting));
        }
        private bool _Connecting;

        public bool Connected {
            get => _Connected;
            private set => Change(ref _Connected, value, nameof(Connected));
        }
        private bool _Connected;

        public bool Disposed {
            get => _Disposed;
            private set => Change(ref _Disposed, value, nameof(Disposed));
        }
        private bool _Disposed;

        public SSHKnownHost KnownHost {
            get => _KnownHost;
            private set => Change(ref _KnownHost, value, nameof(KnownHost));
        }
        private SSHKnownHost _KnownHost;

        public SSHFingerprint HostFingerprint {
            get => _HostFingerprint;
            private set => Change(ref _HostFingerprint, value, nameof(HostFingerprint));
        }
        private SSHFingerprint _HostFingerprint;

        public Uri Uri { get; }

        public SSHClientSession(Uri uri) {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        }

        public async Task<ScpRecv> SCPRecv(string path, CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(SCPRecv), nameof(Lock), path));
            }
            await Lock(token).ConfigureAwait(false);
            var conn = await Conn.Ready(token).ConfigureAwait(false);
            var
            recv = new ScpRecv(conn, path);
            recv.Disposed += (s, e) => {
                if (Log.Info()) {
                    Log.Info(Log.Join(nameof(SCPRecv), nameof(Unlock), path));
                }
                Unlock();
            };
            return recv;
        }

        public async Task<ScpSend> SCPSend(string path, int mode, long size, CancellationToken token) {
            if (Log.Info()) {
                Log.Info(
                    Log.Join(nameof(SCPSend), nameof(Lock), path),
                    Log.Join(nameof(mode), mode),
                    Log.Join(nameof(size), size));
            }
            await Lock(token).ConfigureAwait(false);
            var conn = await Conn.Ready(token).ConfigureAwait(false);
            var
            send = new ScpSend(conn, path, mode, size);
            send.Disposed += (s, e) => {
                if (Log.Info()) {
                    Log.Info(Log.Join(nameof(SCPSend), nameof(Unlock), path));
                }
                Unlock();
            };
            return send;
        }

        public Task Authenticate(SSHAuth auth, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(auth);
            return Lock(token: token, task: async t => {
                var conn = await Conn.Ready(t).ConfigureAwait(false);
                var cancel = BrowsCanceler.From(t);
                var secret = auth.Secret;
                var privateKeyFile = auth.PrivateKeyFile?.Trim() ?? "";
                if (privateKeyFile == "") {
                    await Task
                        .Run(cancellationToken: t, action: () => {
                            conn.AuthByPassword(secret, cancel);
                        })
                        .ConfigureAwait(false);
                }
                else {
                    await Task
                        .Run(cancellationToken: t, action: () => {
                            conn.AuthByKeyFile(auth.PublicKeyFile, privateKeyFile, secret, cancel);
                        })
                        .ConfigureAwait(false);
                }
                return default(object);
            });
        }

        public Task<bool> Authenticated(CancellationToken token) {
            return Lock(token: token, task: async t => {
                var conn = await Conn.Ready(t).ConfigureAwait(false);
                return conn.AuthSuccess();
            });
        }

        public Task<SSHServerResponse> Execute(SSHClientCommand command, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(command);
            return Lock(token: token, task: async t => {
                var conn = await Conn
                    .Ready(t)
                    .ConfigureAwait(false);
                return await Task
                    .Run(cancellationToken: t, function: async () => {
                        using (var chan = new Exec(conn)) {
                            chan.Exec(command.Command, BrowsCanceler.From(token));
                            var stdOut = SSHExecText.StdOut(chan);
                            var stdErr = SSHExecText.StdErr(chan);
                            var stdOutText = await stdOut.DecodeText(command.StdOutBuilder, command.StdOutText, token).ConfigureAwait(false);
                            var stdErrText = await stdErr.DecodeText(command.StdErrBuilder, command.StdErrText, token).ConfigureAwait(false);
                            var response = new SSHServerResponse(stdOutText, stdErrText);
                            return response;
                        }
                    })
                    .ConfigureAwait(false);
            });
        }

        public async Task<object> Connection(CancellationToken token) {
            return await Lock(Conn.Ready, token).ConfigureAwait(false);
        }

        async ValueTask IAsyncDisposable.DisposeAsync() {
            using (Locker) {
                await Lock(token: CancellationToken.None, task: async _ => {
                    var conn = Conn.Result;
                    if (conn != null) {
                        await Task
                            .Run(conn.Dispose, CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    await Conn.Refresh(CancellationToken.None).ConfigureAwait(false);
                    Connected = false;
                    return Disposed = true;
                });
            }
        }
    }
}
