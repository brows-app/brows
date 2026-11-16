using Brows.Entries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Provides operations that create and extract tar archive files for other plugins.
/// </summary>
public static class TarArchiveFile {
    /// <summary>
    /// Adds filesystem entries to an archive, creating the archive when it does not exist.
    /// Entries with names that already exist are replaced; all other archive entries are kept.
    /// </summary>
    /// <remarks>
    /// Each key is an archive-relative entry name, and each value is a filesystem file or directory
    /// path that supplies that entry. A directory path adds an explicit directory entry; this method
    /// does not enumerate the directory's contents. Entry names are compared ordinally.
    /// Entry names must be relative and must not contain parent components. Invalid names throw
    /// <see cref="ArgumentException"/>. Source paths must identify existing files or directories; a
    /// missing source path throws <see cref="FileNotFoundException"/>.
    ///
    /// Cancellation is forwarded while the archive is prepared and written. Cancellation can race
    /// the final commit, so a canceled task does not guarantee that the archive was left unchanged.
    /// </remarks>
    /// <param name="archivePath">
    /// The path of the archive to create or update.
    /// </param>
    /// <param name="files">
    /// The archive-relative entry names and the filesystem paths that supply their contents.
    /// </param>
    /// <param name="progress">
    /// The progress reporter, or <see langword="null"/> when progress is not needed.
    /// </param>
    /// <param name="token">
    /// A token that requests cancellation of the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the archive update finishes.
    /// </returns>
    public static async Task Create(string archivePath,
                                    IReadOnlyDictionary<string, string> files,
                                    IOperationProgress progress,
                                    CancellationToken token) {
        ArgumentNullException.ThrowIfNull(archivePath);
        ArgumentNullException.ThrowIfNull(files);
        var path = await TarArchivePath.GetAsync(new FileInfo(Path.GetFullPath(archivePath)), token);
        try {
            await path.Update(
                progress: progress,
                token: token,
                info: new TarArchiveUpdate {
                    CreateEntriesFromFiles = new Dictionary<string, string>(files),
                });
        }
        finally {
            path.Release();
        }
    }

    /// <summary>
    /// Extracts every item in an archive to the specified directory.
    /// </summary>
    /// <remarks>
    /// Entries that hold file data are extracted to files; entries of links, devices, and fifos are
    /// listed inside the archive but extracted to nothing. Cancellation is forwarded during
    /// extraction. Outputs that were committed before a late cancellation or a later output failure
    /// can remain in the destination directory.
    /// </remarks>
    /// <param name="archivePath">
    /// The path of the archive to extract.
    /// </param>
    /// <param name="directory">
    /// The destination directory for the extracted items.
    /// </param>
    /// <param name="overwrite">
    /// <see langword="true"/> to replace files that already exist in the destination directory.
    /// </param>
    /// <param name="progress">
    /// The progress reporter, or <see langword="null"/> when progress is not needed.
    /// </param>
    /// <param name="token">
    /// A token that requests cancellation of the operation.
    /// </param>
    /// <returns>
    /// A task that completes when extraction finishes.
    /// </returns>
    public static async Task Extract(string archivePath,
                                     string directory,
                                     bool overwrite,
                                     IOperationProgress progress,
                                     CancellationToken token) {
        ArgumentNullException.ThrowIfNull(archivePath);
        ArgumentNullException.ThrowIfNull(directory);
        var path = await TarArchivePath.GetAsync(new FileInfo(Path.GetFullPath(archivePath)), token);
        try {
            await path.ExtractAll(directory, overwrite, progress, token);
        }
        finally {
            path.Release();
        }
    }
}
