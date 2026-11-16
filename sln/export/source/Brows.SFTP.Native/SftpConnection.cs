using Brows.Native;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;

namespace Brows.SFTP.Native;

internal sealed class SftpConnection : NativeType {
    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    private readonly Lock LifetimeLocker = new();
    private bool NativeDisposed;

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Conn_auth_by_password(IntPtr p, IntPtr password, ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Conn_auth_by_key_file(IntPtr p,
                                                              IntPtr publicKeyFile,
                                                              IntPtr privateKeyFile,
                                                              IntPtr passphrase,
                                                              ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Conn_close(IntPtr p, ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Conn_is_aborted(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern void brows_ssh_Conn_request_abort(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Conn_auth_success(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Conn_connect(IntPtr p, ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_Conn_create();

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern void brows_ssh_Conn_destroy(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_Conn_get_fingerprint(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_Conn_get_fingerprint_hash_func(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Conn_get_fingerprint_size(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Conn_set_host_address(IntPtr p,
                                                              IntPtr address,
                                                              uint addressLength,
                                                              int family,
                                                              uint scopeId);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Conn_set_port(IntPtr p, int port);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Conn_set_username(IntPtr p, IntPtr username);

    private static void UseSecret(SecureString secret, Action<IntPtr> action) {
        ArgumentNullException.ThrowIfNull(action);
        if (secret is null) {
            action(IntPtr.Zero);
            return;
        }

        using (var buffer = new SftpSecretBuffer(secret)) {
            action(buffer.Pointer);
        }
    }

    protected sealed override IntPtr Create() {
        return brows_ssh_Conn_create();
    }

    protected sealed override void Dispose(bool disposing) {
        lock (LifetimeLocker) {
            NativeDisposed = true;
            base.Dispose(disposing);
        }
    }

    protected sealed override void Destroy(IntPtr handle) {
        brows_ssh_Conn_destroy(handle);
    }

    internal byte[] Fingerprint {
        get {
            var pointer = brows_ssh_Conn_get_fingerprint(GetHandle());
            var result = new byte[FingerprintSize];
            Marshal.Copy(pointer, result, 0, result.Length);
            return result;
        }
    }

    internal bool IsAborted => brows_ssh_Conn_is_aborted(GetHandle()) != 0;
    internal string FingerprintHashFunc => Marshal.PtrToStringAnsi(brows_ssh_Conn_get_fingerprint_hash_func(GetHandle()));
    internal int FingerprintSize => brows_ssh_Conn_get_fingerprint_size(GetHandle());

    internal void RequestAbort() {
        lock (LifetimeLocker) {
            if (NativeDisposed) {
                return;
            }
            brows_ssh_Conn_request_abort(GetHandle());
        }
    }

    internal void SetAddress(IPAddress address) {
        ArgumentNullException.ThrowIfNull(address);
        var bytes = address.GetAddressBytes();
        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try {
            Try(() => brows_ssh_Conn_set_host_address(
                GetHandle(),
                pin.AddrOfPinnedObject(),
                checked((uint)bytes.Length),
                (int)address.AddressFamily,
                address.AddressFamily == AddressFamily.InterNetworkV6
                    ? checked((uint)address.ScopeId)
                    : 0));
        }
        finally {
            pin.Free();
        }
    }

    internal void SetPort(int port) {
        Try(() => brows_ssh_Conn_set_port(GetHandle(), port));
    }

    internal void SetUsername(string username) {
        using var native = new NativeString(Utf8, username);
        Try(() => brows_ssh_Conn_set_username(GetHandle(), native.Handle));
    }

    internal void Connect(CancellationToken token) {
        var cancel = BrowsCanceler.From(token);
        Try(() => brows_ssh_Conn_connect(GetHandle(), ref cancel));
    }

    internal void Close() {
        var cancel = BrowsCanceler.None;
        Try(() => brows_ssh_Conn_close(GetHandle(), ref cancel));
    }

    internal void Authenticate(SecureString password, CancellationToken token) {
        UseSecret(password, pointer => {
            var cancel = BrowsCanceler.From(token);
            Try(() => brows_ssh_Conn_auth_by_password(GetHandle(), pointer, ref cancel));
        });
    }

    internal void Authenticate(string publicKeyFile,
                               string privateKeyFile,
                               SecureString passphrase,
                               CancellationToken token) {
        using var publicKey = publicKeyFile is null ? null : new NativeString(Utf8, publicKeyFile);
        using var privateKey = new NativeString(Utf8, privateKeyFile);
        UseSecret(passphrase, secret => {
            var cancel = BrowsCanceler.From(token);
            Try(() => brows_ssh_Conn_auth_by_key_file(
                GetHandle(),
                publicKey?.Handle ?? IntPtr.Zero,
                privateKey.Handle,
                secret,
                ref cancel));
        });
    }

    internal bool Authenticated() {
        return brows_ssh_Conn_auth_success(GetHandle()) == 0;
    }

    internal IntPtr Handle => GetHandle();
}
