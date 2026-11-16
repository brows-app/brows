using System;
using System.Collections.Generic;
using System.Text;

namespace Brows.MBox.Format;

/// <summary>
/// Recognizes the envelope line that introduces a stored message.
/// </summary>
internal static class MBoxEnvelopeReader {
    /// <summary>
    /// The header fields that identify a message during recovery.
    /// </summary>
    private static readonly string[] RecoveryFields = [
        "From",
        "Date",
        "Subject",
        "Message-ID",
        "To",
    ];

    /// <summary>
    /// Returns the sender token and the date of an envelope line.
    /// </summary>
    /// <param name="line">
    /// The bytes of the line, without its terminator.
    /// </param>
    /// <param name="length">
    /// The number of bytes of the line to examine.
    /// </param>
    /// <param name="date">
    /// The date of the envelope, or <see langword="null"/> when the date is missing or invalid.
    /// </param>
    /// <param name="diagnostics">
    /// The list that receives the problems found while reading the envelope.
    /// </param>
    /// <param name="offset">
    /// The absolute offset of the envelope line.
    /// </param>
    /// <returns>
    /// The sender token of the envelope, or <see langword="null"/> when the line is not an
    /// envelope whose date is recognized. A line whose date is not recognized may still be an
    /// envelope, which is decided by a bounded header lookahead.
    /// </returns>
    public static string Read(byte[] line,
                              int length,
                              out DateTimeOffset? date,
                              List<MBoxDiagnostic> diagnostics,
                              long offset) {
        date = null;
        if (length < 5) {
            return null;
        }
        /*
         * The envelope starts with the case-sensitive "From " prefix.
         */
        if (line[0] != (byte)'F' ||
            line[1] != (byte)'r' ||
            line[2] != (byte)'o' ||
            line[3] != (byte)'m' ||
            line[4] != (byte)' ') {
            return null;
        }
        var senderEnd = SenderEnd(line, length);
        if (senderEnd <= 5) {
            /*
             * The sender token is not empty.
             */
            return null;
        }
        var sender = Encoding.UTF8.GetString(line, 5, senderEnd - 5);
        /*
         * The date is what follows the whitespace that ends the sender token.
         */
        var rest = TrimStart(line, senderEnd, length);
        var text = Encoding.UTF8.GetString(line, rest.start, rest.length);
        if (MBoxDate.TryParseEnvelope(text, out var parsed, out var weekdayMismatch, out var invalidValue) == false) {
            /*
             * The date is not recognized, so the line may still be an envelope. A bounded header
             * lookahead decides that.
             */
            return null;
        }
        date = parsed;
        if (weekdayMismatch) {
            diagnostics?.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.MalformedDate,
                offset,
                "The weekday of the envelope date does not match the date."));
        }
        if (invalidValue) {
            diagnostics?.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.MalformedDate,
                offset,
                "The envelope date is not a valid date."));
        }
        return NormalizeSender(sender);
    }

    /// <summary>
    /// Returns whether the specified header fields identify a message during recovery.
    /// </summary>
    /// <param name="headers">
    /// The header fields that were read, with their names and values.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the header fields identify a message during recovery.
    /// </returns>
    public static bool IsRecoverable(IReadOnlyList<KeyValuePair<string, string>> headers) {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++) {
            var header = headers[i];
            if (IsThreadId(header.Key) && IsNumeric(header.Value)) {
                return true;
            }
            for (var j = 0; j < RecoveryFields.Length; j++) {
                if (string.Equals(header.Key, RecoveryFields[j], StringComparison.OrdinalIgnoreCase)) {
                    found.Add(RecoveryFields[j]);
                }
            }
        }
        if (found.Count < 2) {
            return false;
        }
        return found.Contains("From") || found.Contains("Message-ID");
    }

    /// <summary>
    /// Returns whether the specified header name is the Google thread identifier.
    /// </summary>
    /// <param name="name">
    /// The name of the header field.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the specified header name is the Google thread identifier.
    /// </returns>
    public static bool IsThreadId(string name) {
        return string.Equals(name, "X-GM-THRID", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the sender token of an envelope line that is recovered.
    /// </summary>
    /// <param name="line">
    /// The bytes of the envelope line.
    /// </param>
    /// <param name="length">
    /// The number of bytes of the envelope line to examine.
    /// </param>
    /// <returns>
    /// The sender token of the envelope.
    /// </returns>
    public static string RecoverSender(byte[] line, int length) {
        var senderEnd = SenderEnd(line, length);
        if (senderEnd <= 5) {
            return string.Empty;
        }
        return NormalizeSender(Encoding.UTF8.GetString(line, 5, senderEnd - 5));
    }

    /// <summary>
    /// Returns the offset of the end of the sender token of an envelope line.
    /// </summary>
    /// <param name="line">
    /// The bytes of the envelope line.
    /// </param>
    /// <param name="length">
    /// The number of bytes of the envelope line to examine.
    /// </param>
    /// <returns>
    /// The offset of the end of the sender token.
    /// </returns>
    private static int SenderEnd(byte[] line, int length) {
        var end = 5;
        while (end < length && IsSpace(line[end]) == false) {
            end++;
        }
        return end;
    }

    private static string NormalizeSender(string sender) {
        /*
         * The mbox null sender carries no address.
         */
        return sender == "-" ? string.Empty : sender;
    }

    private static bool IsSpace(byte value) {
        return value == (byte)' ' || value == (byte)'\t';
    }

    /// <summary>
    /// Returns whether the specified value is a number.
    /// </summary>
    /// <param name="value">
    /// The value to examine. May be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the specified value is a number.
    /// </returns>
    private static bool IsNumeric(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }
        var trimmed = value.Trim();
        for (var i = 0; i < trimmed.Length; i++) {
            if (trimmed[i] < '0' || trimmed[i] > '9') {
                return false;
            }
        }
        return true;
    }

    private static (int start, int length) TrimStart(byte[] line, int start, int end) {
        while (start < end && IsSpace(line[start])) {
            start++;
        }
        while (end > start && IsSpace(line[end - 1])) {
            end--;
        }
        return (start, end - start);
    }
}
