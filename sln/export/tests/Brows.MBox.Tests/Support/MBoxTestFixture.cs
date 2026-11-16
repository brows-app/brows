using System;
using System.IO;
using System.Text;

namespace Brows;

/// <summary>
/// Builds the mailboxes that the tests of the feature read.
/// </summary>
internal static class MBoxTestFixture {
    /// <summary>
    /// The envelope line that introduces a Google Takeout message.
    /// </summary>
    public const string Envelope =
        "From 1512584914615853097@xxx Thu Sep 17 18:09:44 +0000 2015";

    /// <summary>
    /// Creates a mailbox that holds a single message.
    /// </summary>
    /// <param name="body">
    /// The body of the message.
    /// </param>
    /// <param name="headers">
    /// The header fields of the message, without their terminators.
    /// </param>
    /// <param name="terminator">
    /// The line terminator of the mailbox.
    /// </param>
    /// <returns>
    /// The text of the mailbox.
    /// </returns>
    public static string Mailbox(string body,
                                 string[] headers = null,
                                 string terminator = "\n") {
        var builder = new StringBuilder();
        builder.Append(Envelope);
        builder.Append(terminator);
        if (headers != null) {
            foreach (var header in headers) {
                builder.Append(header);
                builder.Append(terminator);
            }
        }
        builder.Append(terminator);
        builder.Append(body);
        return builder.ToString();
    }

    /// <summary>
    /// Creates a mailbox that holds the specified messages, separated by the empty line that
    /// frames the records of a mailbox.
    /// </summary>
    /// <param name="messages">
    /// The bodies of the messages.
    /// </param>
    /// <param name="headers">
    /// The header fields of every message, without their terminators.
    /// </param>
    /// <param name="terminator">
    /// The line terminator of the mailbox.
    /// </param>
    /// <returns>
    /// The text of the mailbox.
    /// </returns>
    public static string Mailbox(string[] messages,
                                 string[] headers = null,
                                 string terminator = "\n") {
        var builder = new StringBuilder();
        var first = true;
        foreach (var message in messages) {
            if (first == false) {
                /*
                 * The empty line that separates two records of a mailbox.
                 */
                builder.Append(terminator);
            }
            first = false;
            builder.Append(Mailbox(message, headers, terminator));
        }
        return builder.ToString();
    }

    /// <summary>
    /// Writes the specified text to a file of the specified directory, and returns the path of
    /// the file.
    /// </summary>
    /// <param name="directory">
    /// The directory that receives the file.
    /// </param>
    /// <param name="name">
    /// The name of the file.
    /// </param>
    /// <param name="text">
    /// The text of the file.
    /// </param>
    /// <returns>
    /// The path of the file.
    /// </returns>
    public static string WriteFile(DirectoryInfo directory, string name, string text) {
        var path = Path.Combine(directory.FullName, name);
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }
}
