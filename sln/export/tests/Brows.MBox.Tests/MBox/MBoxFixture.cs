using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Brows.MBox.Format;

namespace Brows.MBox;

/// <summary>
/// Builds the byte-exact mailboxes that the tests read.
/// </summary>
internal static class MBoxFixture {
    /// <summary>
    /// The envelope line that introduces a Google Takeout message.
    /// </summary>
    public const string Envelope =
        "From 1512584914615853097@xxx Thu Sep 17 18:09:44 +0000 2015";

    /// <summary>
    /// Creates a stream over the specified text.
    /// </summary>
    /// <param name="text">
    /// The text of the mailbox.
    /// </param>
    /// <returns>
    /// A readable and seekable stream over the text.
    /// </returns>
    public static MemoryStream Stream(string text) {
        return new MemoryStream(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// Creates a mailbox with a single message.
    /// </summary>
    /// <param name="body">
    /// The body of the message.
    /// </param>
    /// <param name="headers">
    /// The header fields of the message, without their terminators.
    /// </param>
    /// <param name="envelope">
    /// The envelope line of the message.
    /// </param>
    /// <param name="terminator">
    /// The line terminator of the mailbox.
    /// </param>
    /// <returns>
    /// The text of the mailbox.
    /// </returns>
    public static string Mailbox(string body,
                                 IEnumerable<string> headers = null,
                                 string envelope = Envelope,
                                 string terminator = "\n") {
        var builder = new StringBuilder();
        builder.Append(envelope);
        builder.Append(terminator);
        if (headers is not null) {
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
    /// <param name="envelope">
    /// The envelope line of every message.
    /// </param>
    /// <param name="terminator">
    /// The line terminator of the mailbox.
    /// </param>
    /// <returns>
    /// The text of the mailbox.
    /// </returns>
    public static string Mailbox(IEnumerable<string> messages,
                                 IEnumerable<string> headers = null,
                                 string envelope = Envelope,
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
            builder.Append(Mailbox(message, headers, envelope, terminator));
        }
        return builder.ToString();
    }

    /// <summary>
    /// Creates a mailbox whose body holds the specified bytes, exactly as they are given.
    /// </summary>
    /// <param name="body">
    /// The bytes of the body of the message.
    /// </param>
    /// <param name="headers">
    /// The header fields of the message, without their terminators.
    /// </param>
    /// <param name="terminator">
    /// The line terminator of the mailbox.
    /// </param>
    /// <returns>
    /// The bytes of the mailbox.
    /// </returns>
    public static byte[] MailboxBytes(byte[] body,
                                      IEnumerable<string> headers = null,
                                      string terminator = "\n") {
        var builder = new List<byte>();
        builder.AddRange(Encoding.UTF8.GetBytes(Envelope + terminator));
        if (headers is not null) {
            foreach (var header in headers) {
                builder.AddRange(Encoding.UTF8.GetBytes(header + terminator));
            }
        }
        builder.AddRange(Encoding.UTF8.GetBytes(terminator));
        builder.AddRange(body);
        return [.. builder];
    }

    /// <summary>
    /// Reads a message of a mailbox whose body holds the specified bytes.
    /// </summary>
    /// <param name="body">
    /// The bytes of the body of the message.
    /// </param>
    /// <param name="headers">
    /// The header fields of the message, without their terminators.
    /// </param>
    /// <param name="options">
    /// The options that control how the mailbox is read.
    /// </param>
    /// <returns>
    /// The message of the record.
    /// </returns>
    public static MBoxMessage MessageOfBytes(byte[] body,
                                              IEnumerable<string> headers = null,
                                              MBoxParserOptions options = null) {
        var parser = new MBoxParser(options ?? MBoxParserOptions.Default);
        using var stream = new MemoryStream(MailboxBytes(body, headers));
        var records = new List<MBoxRecord>(parser.Scan(stream, null, CancellationToken.None));
        return parser.ReadMessage(stream, records[0], CancellationToken.None);
    }

    /// <summary>
    /// Reads the records of a mailbox.
    /// </summary>
    /// <param name="text">
    /// The text of the mailbox.
    /// </param>
    /// <param name="options">
    /// The options that control how the mailbox is read.
    /// </param>
    /// <returns>
    /// The records of the mailbox.
    /// </returns>
    public static List<MBoxRecord> Records(string text, MBoxParserOptions options = null) {
        var parser = new MBoxParser(options ?? MBoxParserOptions.Default);
        using var stream = Stream(text);
        return [.. parser.Scan(stream, null, CancellationToken.None)];
    }

    /// <summary>
    /// Reads the records of a mailbox and the diagnostics that were reported.
    /// </summary>
    /// <param name="text">
    /// The text of the mailbox.
    /// </param>
    /// <param name="options">
    /// The options that control how the mailbox is read.
    /// </param>
    /// <returns>
    /// The records of the mailbox, and the diagnostics that were reported.
    /// </returns>
    public static (List<MBoxRecord> Records, List<MBoxDiagnostic> Diagnostics) Scan(
        string text,
        MBoxParserOptions options = null) {
        var parser = new MBoxParser(options ?? MBoxParserOptions.Default);
        var diagnostics = new List<MBoxDiagnostic>();
        using var stream = Stream(text);
        var records = new List<MBoxRecord>(parser.Scan(stream, diagnostics.Add, CancellationToken.None));
        return (records, diagnostics);
    }

    /// <summary>
    /// Reads a message of a mailbox.
    /// </summary>
    /// <param name="text">
    /// The text of the mailbox.
    /// </param>
    /// <param name="index">
    /// The index of the record to read.
    /// </param>
    /// <param name="options">
    /// The options that control how the mailbox is read.
    /// </param>
    /// <returns>
    /// The message of the record.
    /// </returns>
    public static MBoxMessage Message(string text, int index = 0, MBoxParserOptions options = null) {
        var parser = new MBoxParser(options ?? MBoxParserOptions.Default);
        using var stream = Stream(text);
        var records = new List<MBoxRecord>(parser.Scan(stream, null, CancellationToken.None));
        return parser.ReadMessage(stream, records[index], CancellationToken.None);
    }

    /// <summary>
    /// Reads the header fields of a record of a mailbox, without reading its body.
    /// </summary>
    /// <param name="text">
    /// The text of the mailbox.
    /// </param>
    /// <param name="index">
    /// The index of the record to read.
    /// </param>
    /// <param name="options">
    /// The options that control how the mailbox is read.
    /// </param>
    /// <returns>
    /// The message of the record, with a body that was not read.
    /// </returns>
    public static MBoxMessage Headers(string text, int index = 0, MBoxParserOptions options = null) {
        var parser = new MBoxParser(options ?? MBoxParserOptions.Default);
        using var stream = Stream(text);
        var records = new List<MBoxRecord>(parser.Scan(stream, null, CancellationToken.None));
        return parser.ReadHeaders(stream, records[index], CancellationToken.None);
    }

    /// <summary>
    /// Extracts a message of a mailbox as an Internet message.
    /// </summary>
    /// <param name="text">
    /// The text of the mailbox.
    /// </param>
    /// <param name="index">
    /// The index of the record to extract.
    /// </param>
    /// <param name="options">
    /// The options that control how the mailbox is read.
    /// </param>
    /// <returns>
    /// The bytes of the extracted message.
    /// </returns>
    public static string Extract(string text, int index = 0, MBoxParserOptions options = null) {
        var parser = new MBoxParser(options ?? MBoxParserOptions.Default);
        using var stream = Stream(text);
        var records = new List<MBoxRecord>(parser.Scan(stream, null, CancellationToken.None));
        using var destination = new MemoryStream();
        parser.ExtractMessage(stream, records[index], destination, CancellationToken.None);
        return Encoding.UTF8.GetString(destination.ToArray());
    }
}
