using System;
using System.Runtime.InteropServices;

namespace Brows.SevenZip;

/*
 * Mirrors the native brows_7z_UpdateItem structure. Keep field order in sync.
 */
[StructLayout(LayoutKind.Sequential)]
internal struct SevenZipUpdateItemNative {
    public IntPtr Path;
    public IntPtr SourcePath;
    public int IsDirectory;
    public ulong Size;
    public long ModifiedTime;
    public uint Attributes;
}
