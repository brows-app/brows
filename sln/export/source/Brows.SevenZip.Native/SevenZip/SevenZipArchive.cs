using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Brows.SevenZip;

internal sealed partial class SevenZipArchive : NativeType {
    private static readonly Action<IntPtr> NativeCloser = brows_7z_Archive_close;
    private static readonly Action<SevenZipArchive> EntryReader = archive => archive.ReadEntries();

    private readonly Lock Locker = new();
    private readonly Action<IntPtr> CloseNative;
    private IntPtr NativeHandle;
    private bool IsDisposed;

    private SevenZipArchive(IntPtr native, Action<IntPtr> closeNative) {
        NativeHandle = native != IntPtr.Zero
            ? native
            : throw new ArgumentException("A native archive is required.", nameof(native));
        CloseNative = closeNative ?? throw new ArgumentNullException(nameof(closeNative));
    }

    [LibraryImport(SevenZipNative.Dll, StringMarshalling = StringMarshalling.Utf8),
     UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_7z_Archive_open(string path, out nint archive);

    [LibraryImport(SevenZipNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void brows_7z_Archive_close(nint archive);

    [LibraryImport(SevenZipNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial uint brows_7z_Archive_get_entry_count(nint archive);

    [LibraryImport(SevenZipNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nint brows_7z_Archive_get_entry(nint archive, uint index);

    [LibraryImport(SevenZipNative.Dll, StringMarshalling = StringMarshalling.Utf8),
     UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_7z_directory_is_case_sensitive(string directory, out int isCaseSensitive);

    [LibraryImport(SevenZipNative.Dll, StringMarshalling = StringMarshalling.Utf8),
     UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_7z_Archive_extract_entry(
        nint archive,
        uint index,
        string destination,
        int overwrite,
        nint progress,
        nint state);

    [LibraryImport(SevenZipNative.Dll, StringMarshalling = StringMarshalling.Utf8),
     UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_7z_Archive_extract_all(
        nint archive,
        string directory,
        int overwrite,
        nint progress,
        nint state);

    [LibraryImport(SevenZipNative.Dll, StringMarshalling = StringMarshalling.Utf8),
     UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_7z_Archive_create(string path, nint request, nint progress, nint state);

    [LibraryImport(SevenZipNative.Dll, StringMarshalling = StringMarshalling.Utf8),
     UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_7z_Archive_update(string path, nint request, nint progress, nint state);

    [LibraryImport(SevenZipNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void brows_7z_test_set_fault(int point, int kind);

    private static SevenZipArchive OpenCore(IntPtr native,
                                            Action<SevenZipArchive> readEntries,
                                            Action<IntPtr> closeNative) {
        SevenZipArchive archive = null;
        try {
            archive = new SevenZipArchive(native, closeNative);
            readEntries(archive);
            return archive;
        }
        catch {
            if (archive is null) {
                closeNative(native);
            }
            archive?.Dispose();
            throw;
        }
    }

    private static void Write(string path,
                              IReadOnlyList<SevenZipUpdateItem> items,
                              IReadOnlyList<uint> keptIndices,
                              int? compressionLevel,
                              bool update,
                              IOperationProgress progress,
                              CancellationToken token) {
        ArgumentNullException.ThrowIfNull(path);
        token.ThrowIfCancellationRequested();
        using (var request = new SevenZipUpdateRequest(items, keptIndices, compressionLevel)) {
            using (var callback = new SevenZipNativeProgress(progress, token)) {
                callback.ThrowIfCancellationRequested();
                var error = update
                    ? brows_7z_Archive_update(path, request.Handle, callback.Function, callback.State)
                    : brows_7z_Archive_create(path, request.Handle, callback.Function, callback.State);
                callback.ThrowCallbackException();
                callback.ThrowIfCancellationRequested();
                if (error != 0) {
                    throw SevenZipNative.CreateError(error);
                }
            }
        }
    }

    private void ReadEntries() {
        var count = brows_7z_Archive_get_entry_count(GetHandle());
        var entries = new List<SevenZipArchiveEntry>((int)count);
        for (var index = 0u; index < count; index++) {
            var p = brows_7z_Archive_get_entry(GetHandle(), index);
            if (p == IntPtr.Zero) {
                continue;
            }
            var info = Marshal.PtrToStructure<SevenZipEntryInfoNative>(p);
            entries.Add(new SevenZipArchiveEntry(
                path: Marshal.PtrToStringUTF8(info.Path) ?? string.Empty,
                size: (long)info.Size,
                packedSize: (long)info.PackedSize,
                attributes: info.Attributes,
                modifiedTime: ToDateTimeOffset(info.ModifiedTime),
                crc: info.Crc,
                isDirectory: info.IsDirectory != 0));
        }
        Entries = entries;
    }

    private static DateTimeOffset? ToDateTimeOffset(long fileTime) {
        if (fileTime <= 0) {
            return null;
        }
        try {
            return DateTimeOffset.FromFileTime(fileTime);
        }
        catch (ArgumentOutOfRangeException) {
            return null;
        }
    }

    internal enum NativeFaultPoint {
        None,
        AfterArchiveCreated,
        AfterOutputStaged,
        UpdateCallback,
    }

    internal enum NativeFaultKind {
        None,
        BadAlloc,
        LengthError,
    }

    internal static bool IsDirectoryCaseSensitive(string directory) {
        ArgumentNullException.ThrowIfNull(directory);
        var error = brows_7z_directory_is_case_sensitive(directory, out var isCaseSensitive);
        if (error != 0) {
            throw SevenZipNative.CreateError(error);
        }
        return isCaseSensitive != 0;
    }

    internal static SevenZipArchive OpenForTesting(IntPtr native,
                                                   Action<SevenZipArchive> readEntries,
                                                   Action<IntPtr> closeNative) {
        ArgumentNullException.ThrowIfNull(readEntries);
        ArgumentNullException.ThrowIfNull(closeNative);
        return OpenCore(native, readEntries, closeNative);
    }

    internal static void Create(string path,
                                IReadOnlyList<SevenZipUpdateItem> items,
                                int? compressionLevel,
                                IOperationProgress progress,
                                CancellationToken token) {
        Write(
            path,
            items,
            Array.Empty<uint>(),
            compressionLevel,
            update: false,
            progress: progress,
            token: token);
    }

    internal static void Update(string path,
                                IReadOnlyList<uint> keptIndices,
                                IReadOnlyList<SevenZipUpdateItem> items,
                                int? compressionLevel,
                                IOperationProgress progress,
                                CancellationToken token) {
        Write(
            path,
            items,
            keptIndices,
            compressionLevel,
            update: true,
            progress: progress,
            token: token);
    }

    internal void Extract(int index,
                          string destination,
                          bool overwrite,
                          IOperationProgress progress,
                          CancellationToken token) {
        ArgumentNullException.ThrowIfNull(destination);
        token.ThrowIfCancellationRequested();
        using (var callback = new SevenZipNativeProgress(progress, token)) {
            var error = brows_7z_Archive_extract_entry(
                GetHandle(),
                (uint)index,
                destination,
                overwrite ? 1 : 0,
                callback.Function,
                callback.State);
            callback.ThrowCallbackException();
            callback.ThrowIfCancellationRequested();
            if (error != 0) {
                throw SevenZipNative.CreateError(error);
            }
        }
    }

    internal void ExtractAll(string directory,
                             bool overwrite,
                             IOperationProgress progress,
                             CancellationToken token) {
        ArgumentNullException.ThrowIfNull(directory);
        token.ThrowIfCancellationRequested();
        using (var callback = new SevenZipNativeProgress(progress, token)) {
            var error = brows_7z_Archive_extract_all(
                GetHandle(),
                directory,
                overwrite ? 1 : 0,
                callback.Function,
                callback.State);
            callback.ThrowCallbackException();
            callback.ThrowIfCancellationRequested();
            if (error != 0) {
                throw SevenZipNative.CreateError(error);
            }
        }
    }

    internal static void SetNativeFaultForTesting(NativeFaultPoint point, NativeFaultKind kind) {
        brows_7z_test_set_fault((int)point, (int)kind);
    }

    protected sealed override IntPtr Create() {
        lock (Locker) {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            var native = NativeHandle;
            NativeHandle = IntPtr.Zero;
            return native;
        }
    }

    protected sealed override void Destroy(IntPtr handle) {
        lock (Locker) {
            CloseNative(handle);
        }
    }

    protected sealed override void Dispose(bool disposing) {
        lock (Locker) {
            if (IsDisposed == false) {
                IsDisposed = true;
                if (NativeHandle != IntPtr.Zero) {
                    var native = NativeHandle;
                    NativeHandle = IntPtr.Zero;
                    CloseNative(native);
                }
            }
        }
        base.Dispose(disposing);
    }

    public IReadOnlyList<SevenZipArchiveEntry> Entries { get; private set; } =
        Array.Empty<SevenZipArchiveEntry>();

    public static SevenZipArchive Open(string path) {
        ArgumentNullException.ThrowIfNull(path);
        var error = brows_7z_Archive_open(path, out var native);
        if (error != 0) {
            throw SevenZipNative.CreateError(error);
        }
        return OpenCore(native, EntryReader, NativeCloser);
    }

    public static void Create(string path,
                              IReadOnlyList<SevenZipUpdateItem> items,
                              int? compressionLevel) {
        Create(path, items, compressionLevel, progress: null, token: CancellationToken.None);
    }

    public static void Update(string path,
                              IReadOnlyList<uint> keptIndices,
                              IReadOnlyList<SevenZipUpdateItem> items,
                              int? compressionLevel) {
        Update(path, keptIndices, items, compressionLevel, progress: null, token: CancellationToken.None);
    }

    public void Extract(int index, string destination, bool overwrite) {
        Extract(index, destination, overwrite, progress: null, token: CancellationToken.None);
    }

    public void ExtractAll(string directory, bool overwrite) {
        ExtractAll(directory, overwrite, progress: null, token: CancellationToken.None);
    }

    public int IndexOf(string path) {
        ArgumentNullException.ThrowIfNull(path);
        var entries = Entries;
        for (var i = 0; i < entries.Count; i++) {
            if (string.Equals(entries[i].Path, path, StringComparison.Ordinal)) {
                return i;
            }
        }
        return -1;
    }
}
