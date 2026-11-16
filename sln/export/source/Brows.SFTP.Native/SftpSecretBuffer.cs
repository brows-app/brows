using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Brows.SFTP.Native;

internal sealed class SftpSecretBuffer : IDisposable {
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    private readonly byte[] Buffer;
    private GCHandle Pin;

    private void Clear() {
        try {
            Array.Clear(Buffer);
        }
        finally {
            if (Pin.IsAllocated) {
                Pin.Free();
            }
        }
    }

    internal int Length => Buffer.Length - 1;

    internal IntPtr Pointer => Pin.AddrOfPinnedObject();

    internal ReadOnlyMemory<byte> Bytes => Buffer;

    internal SftpSecretBuffer(SecureString secret) {
        ArgumentNullException.ThrowIfNull(secret);

        var bstr = Marshal.SecureStringToBSTR(secret);
        char[] characters = null;
        byte[] bytes = null;
        try {
            characters = new char[secret.Length];
            Marshal.Copy(bstr, characters, 0, characters.Length);

            var byteCount = Utf8.GetByteCount(characters);
            bytes = new byte[checked(byteCount + 1)];
            Utf8.GetBytes(characters, bytes.AsSpan(0, byteCount));

            var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Buffer = bytes;
            Pin = pin;
            bytes = null;
        }
        finally {
            try {
                Marshal.ZeroFreeBSTR(bstr);
            }
            finally {
                if (characters is not null) {
                    Array.Clear(characters);
                }
                if (bytes is not null) {
                    Array.Clear(bytes);
                }
            }
        }
    }

    void IDisposable.Dispose() {
        Clear();
    }
}
