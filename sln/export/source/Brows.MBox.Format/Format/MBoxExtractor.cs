using System;
using System.IO;
using System.Threading;

namespace Brows.MBox.Format;

/// <summary>
/// Streams a stored message as an Internet message, without altering its bytes.
/// </summary>
/// <remarks>
/// The extraction reads the header block of the record as it is stored, and only the body is
/// unquoted. Every other byte, including the line terminators, the MIME boundaries, the transfer
/// encodings, and the attachment bytes, is copied as it is.
/// </remarks>
internal static class MBoxExtractor {
    /// <summary>
    /// Extracts a record as an Internet message.
    /// </summary>
    /// <param name="source">
    /// The mailbox to read from.
    /// </param>
    /// <param name="record">
    /// The record to extract.
    /// </param>
    /// <param name="destination">
    /// The stream that receives the message.
    /// </param>
    /// <param name="options">
    /// The options that control how the mailbox is read.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the extraction.
    /// </param>
    public static void Extract(Stream source,
                               MBoxRecord record,
                               Stream destination,
                               MBoxParserOptions options,
                               CancellationToken token) {
        var start = record.MessageOffset;
        var end = checked(start + record.MessageLength);
        if (start < 0 || end < start || end > source.Length) {
            throw new ArgumentOutOfRangeException(
                nameof(record),
                "The record does not fit in the source.");
        }
        var reader = new MBoxLineReader(source, options.BufferSize) {
            Limit = end,
        };
        reader.Seek(start, token);
        var copy = new MBoxCopySink(destination);
        var ended = false;
        while (reader.Position < end) {
            token.ThrowIfCancellationRequested();
            if (reader.TryReadLine(0, copy, out var line, token) == false) {
                break;
            }
            if (line.Length == 0) {
                /*
                 * The empty line that ends the header block is part of the message, so it is
                 * written as it is.
                 */
                ended = true;
                break;
            }
        }
        if (ended == false) {
            /*
             * The header block has no terminator, so its remaining bytes are preserved as-is.
             */
            return;
        }
        var unquoting = new MBoxUnquotingSink(destination, record.Format);
        while (reader.Position < end) {
            token.ThrowIfCancellationRequested();
            if (reader.TryReadLine(0, unquoting, out _, token) == false) {
                break;
            }
        }
    }
}
