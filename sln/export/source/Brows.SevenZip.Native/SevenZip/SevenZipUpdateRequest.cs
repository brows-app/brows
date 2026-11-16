using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Brows.SevenZip;

/*
 * The native library reads this description while it writes the archive, so
 * this object owns the unmanaged memory until the write has completed.
 */
internal sealed class SevenZipUpdateRequest : IDisposable {
    private readonly List<IntPtr> Strings = new();
    private readonly IntPtr Items;
    private readonly IntPtr KeptIndices;
    private readonly IntPtr Request;
    private bool IsDisposed;

    public IntPtr Handle {
        get {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return Request;
        }
    }

    public SevenZipUpdateRequest(IReadOnlyList<SevenZipUpdateItem> items,
                                 IReadOnlyList<uint> keptIndices,
                                 int? compressionLevel) {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(keptIndices);
        var itemSize = Marshal.SizeOf<SevenZipUpdateItemNative>();
        Items = items.Count > 0
            ? Marshal.AllocHGlobal(itemSize * items.Count)
            : IntPtr.Zero;
        for (var i = 0; i < items.Count; i++) {
            var item = items[i];
            var path = Marshal.StringToCoTaskMemUTF8(item.Path);
            Strings.Add(path);
            var sourcePath = IntPtr.Zero;
            if (item.SourcePath is not null) {
                sourcePath = Marshal.StringToCoTaskMemUTF8(item.SourcePath);
                Strings.Add(sourcePath);
            }
            var native = new SevenZipUpdateItemNative {
                Path = path,
                SourcePath = sourcePath,
                IsDirectory = item.IsDirectory ? 1 : 0,
                Size = (ulong)item.Size,
                ModifiedTime = item.ModifiedTime?.ToFileTime() ?? 0,
                Attributes = item.Attributes
            };
            Marshal.StructureToPtr(native, Items + (i * itemSize), false);
        }
        KeptIndices = keptIndices.Count > 0
            ? Marshal.AllocHGlobal(sizeof(uint) * keptIndices.Count)
            : IntPtr.Zero;
        if (keptIndices.Count > 0) {
            var indices = new uint[keptIndices.Count];
            for (var i = 0; i < keptIndices.Count; i++) {
                indices[i] = keptIndices[i];
            }
            var bytes = MemoryMarshal.AsBytes(indices.AsSpan()).ToArray();
            /*
             * The overload of Marshal.Copy that takes an array of bytes counts
             * the bytes that are copied, not the items of the array.
             */
            Marshal.Copy(bytes, 0, KeptIndices, bytes.Length);
        }
        Request = Marshal.AllocHGlobal(Marshal.SizeOf<SevenZipUpdateRequestNative>());
        var request = new SevenZipUpdateRequestNative {
            Items = Items,
            ItemCount = (uint)items.Count,
            KeptIndices = KeptIndices,
            KeptCount = (uint)keptIndices.Count,
            CompressionLevel = compressionLevel ?? -1
        };
        Marshal.StructureToPtr(request, Request, false);
    }

    public void Dispose() {
        if (IsDisposed) {
            return;
        }
        IsDisposed = true;
        foreach (var pointer in Strings) {
            Marshal.FreeCoTaskMem(pointer);
        }
        Strings.Clear();
        if (Items != IntPtr.Zero) {
            Marshal.FreeHGlobal(Items);
        }
        if (KeptIndices != IntPtr.Zero) {
            Marshal.FreeHGlobal(KeptIndices);
        }
        if (Request != IntPtr.Zero) {
            Marshal.FreeHGlobal(Request);
        }
    }
}
