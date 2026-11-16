using System;
using System.Runtime.InteropServices;

namespace Brows.SevenZip;

/*
 * Mirrors the native brows_7z_UpdateRequest structure. Keep field order in sync.
 */
[StructLayout(LayoutKind.Sequential)]
internal struct SevenZipUpdateRequestNative {
    public IntPtr Items;
    public uint ItemCount;
    public IntPtr KeptIndices;
    public uint KeptCount;
    public int CompressionLevel;
}
