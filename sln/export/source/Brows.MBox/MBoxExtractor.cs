using Brows.MBox.Format;
using System;
using System.IO;
using System.Threading;

namespace Brows;

/// <summary>
/// Extracts the messages of a mailbox as Internet messages, without altering their bytes.
/// </summary>
internal static class MBoxExtractor {
    /// <summary>
    /// Extracts a message to the specified file.
    /// </summary>
    /// <param name="entry">
    /// The message to extract.
    /// </param>
    /// <param name="path">
    /// The path of the file that receives the message.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the extraction.
    /// </param>
    public static void Extract(MBoxEntry entry, string path, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var session = entry.Session;
        using var lease = session.TryAcquireLease();
        if (lease is null) {
            throw new InvalidOperationException("The mailbox of the message was already retired.");
        }
        var directory = System.IO.Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory) == false) {
            Directory.CreateDirectory(directory);
        }
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try {
            session.File.ExtractMessage(entry.Record, stream, token);
        }
        catch {
            stream.Dispose();
            TryRemove(path);
            throw;
        }
    }

    /// <summary>
    /// Returns the path of the temporary file that holds the extracted message.
    /// </summary>
    /// <param name="sessionId">
    /// The identifier of the session that owns the temporary directory.
    /// </param>
    /// <param name="offset">
    /// The offset of the message in the mailbox.
    /// </param>
    /// <returns>
    /// The path of the temporary file that holds the extracted message.
    /// </returns>
    public static string TempPath(string sessionId, long offset) {
        return TempPath(sessionId, offset, MBoxTemp.Root);
    }

    internal static string TempPath(string sessionId, long offset, string root) {
        return Path.Combine(
            MBoxPath.SessionDirectory(sessionId, root),
            MBoxPath.MessageName(offset));
    }

    private static void TryRemove(string path) {
        try {
            File.Delete(path);
        }
        catch (IOException) {
        }
        catch (UnauthorizedAccessException) {
        }
    }
}
