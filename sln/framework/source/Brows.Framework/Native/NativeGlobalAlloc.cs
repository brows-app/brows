using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Brows.Native;

public readonly struct NativeGlobalAlloc : IDisposable {
    private NativeGlobalAlloc(IntPtr hGlobal) {
        HGlobal = hGlobal;
    }

    void IDisposable.Dispose() {
        if (HGlobal != IntPtr.Zero) {
            Marshal.FreeHGlobal(HGlobal);
        }
    }

    public readonly IntPtr HGlobal { get; }

    public readonly struct OfSecureString : IDisposable {
        private bool Unicode { get; }

        private OfSecureString(IntPtr hGlobal, bool unicode) {
            HGlobal = hGlobal;
            Unicode = unicode;
        }

        internal static OfSecureString Create(SecureString s, CharSet charSet) {
            switch (charSet) {
                case CharSet.Ansi:
                    return new OfSecureString(
                        hGlobal: s == null ? IntPtr.Zero : Marshal.SecureStringToGlobalAllocAnsi(s),
                        unicode: false);
                case CharSet.Unicode:
                    return new OfSecureString(
                        hGlobal: s == null ? IntPtr.Zero : Marshal.SecureStringToGlobalAllocUnicode(s),
                        unicode: true);
                default:
                    throw new ArgumentOutOfRangeException(paramName: nameof(charSet));
            }
        }

        public readonly IntPtr HGlobal { get; }

        void IDisposable.Dispose() {
            if (HGlobal != IntPtr.Zero) {
                if (Unicode) {
                    Marshal.ZeroFreeGlobalAllocUnicode(HGlobal);
                }
                else {
                    Marshal.ZeroFreeGlobalAllocAnsi(HGlobal);
                }
            }
        }
    }

    public static NativeGlobalAlloc String(string s, CharSet charSet) {
        switch (charSet) {
            case CharSet.Ansi:
                return new NativeGlobalAlloc(s == null ? IntPtr.Zero : Marshal.StringToHGlobalAnsi(s));
            case CharSet.Unicode:
                return new NativeGlobalAlloc(s == null ? IntPtr.Zero : Marshal.StringToHGlobalUni(s));
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(charSet));
        }
    }

    public static OfSecureString SecureString(SecureString s, CharSet charSet) {
        return OfSecureString.Create(s, charSet);
    }
}
