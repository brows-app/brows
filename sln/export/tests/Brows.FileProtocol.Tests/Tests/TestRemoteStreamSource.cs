using Brows.Entries;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Tests;

internal sealed class TestRemoteStreamSource : IEntryStreamSource {
    private readonly TestRemoteFileSystem FileSystem;
    private readonly string Path;
    private readonly Func<bool> ReadinessIsDisposed;

    public string EntryID { get; }
    public string EntryName => System.IO.Path.GetFileName(Path);
    public string RelativePath { get; }
    public string SourceFile => null;
    public string SourceDirectory { get; }
    public long StreamLength => FileSystem.Read(Path).Length;
    public Stream Stream { get; private set; }
    public int OpenCount { get; private set; }

    public TestRemoteStreamSource(TestRemoteFileSystem fileSystem,
                                  string entryID,
                                  string path,
                                  string relativePath,
                                  string sourceDirectory = null,
                                  Func<bool> readinessIsDisposed = null) {
        FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        EntryID = entryID ?? throw new ArgumentNullException(nameof(entryID));
        Path = path ?? throw new ArgumentNullException(nameof(path));
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        SourceDirectory = sourceDirectory;
        ReadinessIsDisposed = readinessIsDisposed;
    }

    public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        var readinessIsDisposed = ReadinessIsDisposed?.Invoke() == true;
        if (readinessIsDisposed) {
            throw new InvalidOperationException("The stream set readiness was disposed before stream consumption.");
        }
        if (SourceDirectory is null) {
            OpenCount++;
            Stream = FileSystem.OpenRead(Path);
        }
        else {
            Stream = null;
        }
        return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
    }
}
