using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Brows.Native;

/// <summary>
/// Owns a string in unmanaged memory that must be released through <see cref="IDisposable.Dispose"/>.
/// </summary>
/// <remarks>
/// Copies refer to the same allocation. Dispose only once, after all native uses have finished.
/// </remarks>
public readonly struct NativeGlobalAlloc : IDisposable {
    private NativeGlobalAlloc(IntPtr hGlobal) {
        HGlobal = hGlobal;
    }

    void IDisposable.Dispose() {
        if (HGlobal != IntPtr.Zero) {
            Marshal.FreeHGlobal(HGlobal);
        }
    }

    /// <summary>
    /// Gets the address of the string, or zero for a null string.
    /// </summary>
    public readonly IntPtr HGlobal { get; }

    /// <summary>
    /// Owns an unmanaged copy of a secure string that is cleared and released when disposed.
    /// </summary>
    /// <remarks>
    /// Copies refer to the same allocation. Dispose only once, after all native uses have finished.
    /// </remarks>
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

        /// <summary>
        /// Gets the address of the string, or zero for a null string.
        /// </summary>
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

    /// <summary>
    /// Copies a string to unmanaged memory owned by the returned value.
    /// </summary>
    /// <param name="s">
    /// The string to copy, or null for a zero address.
    /// </param>
    /// <param name="charSet">
    /// Ansi or Unicode, matching the native function that will read the string.
    /// </param>
    /// <returns>
    /// The allocated string. Dispose it through IDisposable when native code no longer needs it.
    /// </returns>
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

    /// <summary>
    /// Copies a string to unmanaged memory owned by the returned value.
    /// </summary>
    /// <param name="s">
    /// The string to copy, or null for a zero address.
    /// </param>
    /// <param name="charSet">
    /// Ansi or Unicode, matching the native function that will read the string.
    /// </param>
    /// <returns>
    /// The allocated string. Dispose it through IDisposable when native code no longer needs it.
    /// </returns>
    public static OfSecureString SecureString(SecureString s, CharSet charSet) {
        return OfSecureString.Create(s, charSet);
    }
}
