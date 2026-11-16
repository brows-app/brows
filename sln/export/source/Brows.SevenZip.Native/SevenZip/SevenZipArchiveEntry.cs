using System;

namespace Brows.SevenZip;

internal sealed record SevenZipArchiveEntry {
    public string Path { get; }

    public long Size { get; }

    public long PackedSize { get; }

    public uint Attributes { get; }

    public DateTimeOffset? ModifiedTime { get; }

    public uint Crc { get; }

    public bool IsDirectory { get; }

    public SevenZipArchiveEntry(string path,
                                long size,
                                long packedSize,
                                uint attributes,
                                DateTimeOffset? modifiedTime,
                                uint crc,
                                bool isDirectory) {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Size = size;
        PackedSize = packedSize;
        Attributes = attributes;
        ModifiedTime = modifiedTime;
        Crc = crc;
        IsDirectory = isDirectory;
    }
}
