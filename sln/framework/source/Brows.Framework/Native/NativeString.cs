using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;

namespace Brows.Native;

/// <summary>
/// Keeps an encoded, null-terminated string available to native code until disposed.
/// </summary>
public sealed class NativeString : CriticalFinalizerObject, IDisposable {
    private byte[] Bytes => _Bytes ??= Encoding.GetBytes(String + '\0');
    private byte[] _Bytes;

    private GCHandle Pin => _Pin ??= GCHandle.Alloc(Bytes, GCHandleType.Pinned);
    private GCHandle? _Pin;

    private void Dispose(bool disposing) {
        if (_Pin.HasValue && _Pin.Value.IsAllocated) {
            _Pin.Value.Free();
        }
    }

    /// <summary>
    /// Gets the address of the encoded string. Use it only while this object is alive and undisposed.
    /// </summary>
    public IntPtr Handle => _Handle ??= Pin.AddrOfPinnedObject();
    private IntPtr? _Handle;

    /// <summary>
    /// Gets the original managed string.
    /// </summary>
    public string String { get; }

    /// <summary>
    /// Gets the encoding used for the native string.
    /// </summary>
    public Encoding Encoding { get; }

    /// <summary>
    /// Initializes a string for native access with the specified encoding.
    /// </summary>
    /// <param name="encoding">
    /// The encoding expected by native code.
    /// </param>
    /// <param name="string">
    /// The string to encode. Null is represented as an empty, null-terminated string.
    /// </param>
    public NativeString(Encoding encoding, string @string) {
        Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        String = @string;
    }

    /// <summary>
    /// Releases the memory held for native access.
    /// </summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases memory held for native access during finalization.
    /// </summary>
    ~NativeString() {
        Dispose(false);
    }
}
