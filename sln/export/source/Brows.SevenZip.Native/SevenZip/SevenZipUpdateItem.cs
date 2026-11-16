using System;

namespace Brows.SevenZip;

internal sealed record SevenZipUpdateItem {
    public string Path { get; }

    public string SourcePath { get; }

    public bool IsDirectory { get; }

    public long Size { get; }

    public DateTimeOffset? ModifiedTime { get; }

    public uint Attributes { get; }

    public SevenZipUpdateItem(string path,
                              string sourcePath,
                              bool isDirectory,
                              long size,
                              DateTimeOffset? modifiedTime,
                              uint attributes) {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        SourcePath = sourcePath;
        IsDirectory = isDirectory;
        Size = size;
        ModifiedTime = modifiedTime;
        Attributes = attributes;
    }
}
