using Domore.Logs;
using System.Collections.Generic;
using System.IO;

namespace Brows.Zip;

internal sealed class ArchiveUpdateStaging : IDisposable {
    private static readonly ILog Log = Logging.For(typeof(ArchiveUpdateStaging));

    private readonly HashSet<string> FileSet = new(StringComparer.OrdinalIgnoreCase);

    private bool Disposed;

    private FileStream CreateFile(string directoryName, string prefix) {
        for (; ; ) {
            var fileName = Path.Combine(directoryName, prefix + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                var file = new FileStream(
                    fileName,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                FileSet.Add(fileName);
                return file;
            }
            catch (IOException) when (File.Exists(fileName)) {
            }
        }
    }

    internal string ArchiveFileName { get; private set; }

    internal FileStream CreateSourceFile() {
        ObjectDisposedException.ThrowIf(Disposed, this);
        return CreateFile(Path.GetTempPath(), "brows-zip-update-");
    }

    internal FileStream CreateArchiveFile(FileInfo archiveFile) {
        ArgumentNullException.ThrowIfNull(archiveFile);
        ObjectDisposedException.ThrowIf(Disposed, this);
        var directoryName = archiveFile.DirectoryName
            ?? throw new ArgumentException("The archive must have a parent directory.", nameof(archiveFile));
        var file = CreateFile(directoryName, ".brows-update-");
        ArchiveFileName = file.Name;
        return file;
    }

    internal void CommitArchive(string destinationFileName) {
        ArgumentNullException.ThrowIfNull(destinationFileName);
        ObjectDisposedException.ThrowIf(Disposed, this);
        var archiveFileName = ArchiveFileName
            ?? throw new InvalidOperationException("There is no staged archive to commit.");
        File.Replace(archiveFileName, destinationFileName, destinationBackupFileName: null);
        FileSet.Remove(archiveFileName);
        ArchiveFileName = null;
    }

    public void Dispose() {
        if (Disposed) {
            return;
        }
        Disposed = true;
        foreach (var file in FileSet) {
            try {
                File.Delete(file);
            }
            catch (Exception ex) {
                if (Log.Warn()) {
                    Log.Warn(ex);
                }
            }
        }
        FileSet.Clear();
    }
}
