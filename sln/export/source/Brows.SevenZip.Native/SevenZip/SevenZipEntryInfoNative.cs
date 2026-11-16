using System;
using System.Runtime.InteropServices;

namespace Brows.SevenZip;

/*
 * Mirrors the native brows_7z_EntryInfo structure. Keep field order in sync.
 */
[StructLayout(LayoutKind.Sequential)]
internal struct SevenZipEntryInfoNative {
    public IntPtr Path;
    public ulong Size;
    public ulong PackedSize;
    public uint Attributes;
    public long ModifiedTime;
    public uint Crc;
    public int IsDirectory;
}
