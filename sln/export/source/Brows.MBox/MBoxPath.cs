using System.IO;

namespace Brows;

/// <summary>
/// Holds the rules that decide whether a path is a mailbox, and the names of the files that the
/// feature uses.
/// </summary>
internal static class MBoxPath {
    /// <summary>
    /// Gets the extension of a mailbox file.
    /// </summary>
    public const string Extension = ".mbox";

    /// <summary>
    /// Gets the name of the directory that holds the temporary files of the feature.
    /// </summary>
    public const string DirectoryName = "MBox";

    /// <summary>
    /// Returns whether the specified path names a mailbox.
    /// </summary>
    /// <param name="path">
    /// The path to check.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the specified path names a mailbox.
    /// </returns>
    public static bool IsMailbox(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return false;
        }
        if (path.EndsWith(".mbox.gz", StringComparison.OrdinalIgnoreCase)) {
            /*
             * A compressed mailbox is not supported.
             */
            return false;
        }
        return path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the name of the file that holds a message, which is derived from the offset of the
    /// message in the mailbox.
    /// </summary>
    /// <param name="offset">
    /// The offset of the message in the mailbox.
    /// </param>
    /// <returns>
    /// The name of the file that holds the message.
    /// </returns>
    public static string MessageName(long offset) {
        return $"message-{offset.ToString(System.Globalization.CultureInfo.InvariantCulture)}.eml";
    }

    public static string ArchiveName(long offset) {
        return $"message-{offset.ToString(System.Globalization.CultureInfo.InvariantCulture)}.zip";
    }

    /// <summary>
    /// Returns the directory that holds the temporary files of the specified session.
    /// </summary>
    /// <param name="sessionId">
    /// The identifier of the session.
    /// </param>
    /// <returns>
    /// The directory that holds the temporary files of the session.
    /// </returns>
    public static string SessionDirectory(string sessionId) {
        return SessionDirectory(sessionId, Path.Combine(
            Path.GetTempPath(),
            "Brows",
            DirectoryName));
    }

    public static string SessionDirectory(string sessionId, string root) {
        return Path.Combine(
            root ?? string.Empty,
            sessionId ?? string.Empty);
    }
}
