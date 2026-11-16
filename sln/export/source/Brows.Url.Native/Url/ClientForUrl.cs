using Domore.Logs;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Brows.Url;

public sealed class ClientForUrl : NativeType {
    private static readonly ILog Log = Logging.For(typeof(ClientForUrl));
    private const int CurlReadFuncAbort = 0x10000000;

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern IntPtr brows_url_ClientForUrl_create(IntPtr url);

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern void brows_url_ClientForUrl_destroy(IntPtr p);

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern IntPtr brows_url_ClientForUrl_error_string(IntPtr p);

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern int brows_url_ClientForUrl_on_header(IntPtr p, IntPtr callback);

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern int brows_url_ClientForUrl_on_write(IntPtr p, IntPtr callback);

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern int brows_url_ClientForUrl_on_read(IntPtr p, IntPtr callback);

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern int brows_url_ClientForUrl_on_progress(IntPtr p, IntPtr callback);

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern int brows_url_ClientForUrl_password(IntPtr p, IntPtr value);

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern int brows_url_ClientForUrl_txrx(IntPtr p);

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern int brows_url_ClientForUrl_username(IntPtr p, IntPtr value);

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern int brows_url_ClientForUrl_ftp_tls(IntPtr p, FtpNativeTlsMode mode);

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern int brows_url_ClientForUrl_ftp_ca_info(IntPtr p, IntPtr path);

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern int brows_url_ClientForUrl_ftp_upload(IntPtr p, long length, int append);

    [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
    private static extern int brows_url_ClientForUrl_ftp_command(IntPtr p,
                                                                 FtpNativeCommand command,
                                                                 IntPtr firstPath,
                                                                 IntPtr secondPath);

    [UnmanagedFunctionPointer(UrlNative.Call)]
    private delegate nuint NativeReadCallback(IntPtr p, IntPtr buffer, nuint length);

    [UnmanagedFunctionPointer(UrlNative.Call)]
    private delegate int NativeProgressCallback(IntPtr p, long downloadTotal, long downloaded, long uploadTotal, long uploaded);

    private readonly ClientDataCallbackWrapper WriteCallback;
    private readonly ClientDataCallbackWrapper HeaderCallback;
    private NativeReadCallback ReadCallback;
    private NativeProgressCallback ProgressCallback;

    private string UriString => _UriString ??= Uri.AbsoluteUri;
    private string _UriString;

    private NativeString UriAlloc => _UriAlloc ??= new(Encoding.UTF8, UriString);
    private NativeString _UriAlloc;

    internal new void Try(Func<IntPtr, int> action, Func<int, Exception> exceptionFactory = null) {
        base.Try(action, exceptionFactory);
    }

    protected sealed override void Dispose(bool disposing) {
        if (disposing) {
            using (_UriAlloc) {
            }
        }
        base.Dispose(disposing);
    }

    protected sealed override nint Create() {
        return brows_url_ClientForUrl_create(UriAlloc.Handle);
    }

    protected sealed override void Destroy(nint handle) {
        brows_url_ClientForUrl_destroy(handle);
    }

    public Encoding Encoding { get; } = Encoding.UTF8;

    public Uri Uri { get; }

    public ClientForUrl(Uri uri) {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        WriteCallback = new() { PointerCreated = p => Try(h => brows_url_ClientForUrl_on_write(h, p)) };
        HeaderCallback = new() { PointerCreated = p => Try(h => brows_url_ClientForUrl_on_header(h, p)) };
    }

    public void Username(string value) {
        using (var s = new NativeString(Encoding, value)) {
            Try(p => brows_url_ClientForUrl_username(p, s.Handle));
        }
    }

    public void Password(string value) {
        using (var s = new NativeString(Encoding, value)) {
            Try(p => brows_url_ClientForUrl_password(p, s.Handle));
        }
    }

    public void OnWrite(ClientDataCallback callback) {
        WriteCallback.Value = callback;
    }

    public void OnHeader(ClientDataCallback callback) {
        HeaderCallback.Value = callback;
    }

    internal void OnRead(Func<IntPtr, nuint, nuint> callback) {
        ArgumentNullException.ThrowIfNull(callback);
        ReadCallback = (p, buffer, length) => {
            try {
                return callback(buffer, length);
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(ex);
                }
                return (nuint)CurlReadFuncAbort;
            }
        };
        var pointer = Marshal.GetFunctionPointerForDelegate(ReadCallback);
        Try(p => brows_url_ClientForUrl_on_read(p, pointer));
    }

    internal void OnProgress(CancellationToken token) {
        ProgressCallback = (p, downloadTotal, downloaded, uploadTotal, uploaded) => token.IsCancellationRequested ? 1 : 0;
        var pointer = Marshal.GetFunctionPointerForDelegate(ProgressCallback);
        Try(p => brows_url_ClientForUrl_on_progress(p, pointer));
    }

    internal void FtpTls(FtpNativeTlsMode mode) {
        Try(p => brows_url_ClientForUrl_ftp_tls(p, mode));
    }

    internal void FtpCertificateAuthorityFile(string path) {
        using var nativePath = new NativeString(Encoding, path);
        Try(p => brows_url_ClientForUrl_ftp_ca_info(p, nativePath.Handle));
    }

    internal void FtpUpload(long length, bool append) {
        Try(p => brows_url_ClientForUrl_ftp_upload(p, length, append ? 1 : 0));
    }

    internal void FtpCommand(FtpNativeCommand command, string firstPath, string secondPath) {
        using var nativeFirstPath = new NativeString(Encoding, firstPath);
        using var nativeSecondPath = secondPath is null ? null : new NativeString(Encoding, secondPath);
        Try(p => brows_url_ClientForUrl_ftp_command(
            p,
            command,
            nativeFirstPath.Handle,
            nativeSecondPath?.Handle ?? IntPtr.Zero));
    }

    public void TxRx() {
        Try(brows_url_ClientForUrl_txrx);
    }

    public string ErrorString() {
        var s = default(IntPtr);
        Try(p => {
            s = brows_url_ClientForUrl_error_string(p);
            return 0;
        });
        return Marshal.PtrToStringUTF8(s);
    }
}
