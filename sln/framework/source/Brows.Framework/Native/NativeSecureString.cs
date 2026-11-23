using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Brows.Native;

/// <summary>
/// Keeps an encoded, null-terminated password available to native code until disposed.
/// </summary>
public sealed class NativeSecureString : CriticalFinalizerObject, IDisposable {
    private byte[] GetBytes() {
        var bstr = Marshal.SecureStringToBSTR(SecureString);
        try {
            var s = Marshal.PtrToStringBSTR(bstr);
            var b = Encoding.GetBytes(s + '\0');
            return b;
        }
        finally {
            Marshal.ZeroFreeBSTR(bstr);
        }
    }

    private byte[] Bytes => _Bytes ??= GetBytes();
    private byte[] _Bytes;

    private GCHandle Pin => _Pin ??= GCHandle.Alloc(Bytes, GCHandleType.Pinned);
    private GCHandle? _Pin;

    private void Dispose(bool disposing) {
        if (_Pin.HasValue && _Pin.Value.IsAllocated) {
            _Pin.Value.Free();
        }
        if (_Bytes != null) {
            Array.Clear(_Bytes);
        }
    }

    /// <summary>
    /// Gets the address of the encoded password. Use it only while this object is alive and undisposed.
    /// </summary>
    public IntPtr Handle => _Handle ??= Pin.AddrOfPinnedObject();
    private IntPtr? _Handle;

    /// <summary>
    /// Gets the encoding used for the native password.
    /// </summary>
    public Encoding Encoding { get; }

    /// <summary>
    /// Gets the secure string supplied at construction.
    /// </summary>
    public SecureString SecureString { get; }

    /// <summary>
    /// Initializes a password for native access with the specified encoding.
    /// </summary>
    /// <param name="encoding">
    /// The encoding expected by native code.
    /// </param>
    /// <param name="secureString">
    /// The password to encode.
    /// </param>
    /// <remarks>
    /// The caller retains ownership of the supplied secure string.
    /// </remarks>
    public NativeSecureString(Encoding encoding, SecureString secureString) {
        Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        SecureString = secureString;
    }

    /// <summary>
    /// Releases native access to the password and clears its encoded bytes.
    /// </summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases native access to the password and clears its encoded bytes during finalization.
    /// </summary>
    ~NativeSecureString() {
        Dispose(false);
    }
}
