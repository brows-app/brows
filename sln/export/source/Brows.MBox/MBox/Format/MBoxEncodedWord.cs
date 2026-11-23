using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Brows.MBox.Format;

/// <summary>
/// Decodes RFC 2047 encoded words in header field values.
/// </summary>
internal static class MBoxEncodedWord {
    private const int MaxEncodedWordLength = 75;

    /// <summary>
    /// Decodes the encoded words of an unstructured header field value.
    /// </summary>
    /// <param name="value">
    /// The raw field value.
    /// </param>
    /// <param name="diagnostics">
    /// The list that receives the problems found while decoding.
    /// </param>
    /// <param name="offset">
    /// The absolute offset used to report diagnostics.
    /// </param>
    /// <returns>
    /// The decoded field value.
    /// </returns>
    internal static string DecodeUnstructured(string value,
                                            List<MBoxDiagnostic> diagnostics,
                                            long offset) {
        if (string.IsNullOrEmpty(value)) {
            return value ?? string.Empty;
        }
        if (value.IndexOf("=?", StringComparison.Ordinal) < 0) {
            return value;
        }
        var builder = new StringBuilder(value.Length);
        var index = 0;
        var afterEncodedWord = false;
        while (index < value.Length) {
            var start = value.IndexOf("=?", index, StringComparison.Ordinal);
            if (start < 0) {
                builder.Append(value, index, value.Length - index);
                break;
            }
            var gap = value.Substring(index, start - index);
            if (afterEncodedWord && IsWhiteSpace(gap)) {
                /*
                 * The whitespace that separates two adjacent encoded words is ignored.
                 */
                afterEncodedWord = false;
            }
            else {
                builder.Append(gap);
            }
            var word = FindWord(value, start);
            if (word is null) {
                /*
                 * The opening marker is not a complete encoded word; keep the text as it is.
                 */
                builder.Append(value, start, value.Length - start);
                break;
            }
            var decoded = DecodeWord(word, diagnostics, offset);
            builder.Append(decoded);
            afterEncodedWord = true;
            index = word.End;
        }
        return builder.ToString();
    }

    /// <summary>
    /// Decodes the display name phrases of an address header field value, leaving the
    /// address specifications untouched.
    /// </summary>
    /// <param name="value">
    /// The raw field value.
    /// </param>
    /// <param name="diagnostics">
    /// The list that receives the problems found while decoding.
    /// </param>
    /// <param name="offset">
    /// The absolute offset used to report diagnostics.
    /// </param>
    /// <returns>
    /// The decoded field value.
    /// </returns>
    internal static string DecodeAddress(string value,
                                       List<MBoxDiagnostic> diagnostics,
                                       long offset) {
        if (string.IsNullOrEmpty(value)) {
            return value ?? string.Empty;
        }
        if (value.IndexOf("=?", StringComparison.Ordinal) < 0) {
            return value;
        }
        var builder = new StringBuilder(value.Length);
        var index = 0;
        var afterEncodedWord = false;
        while (index < value.Length) {
            var character = value[index];
            var previousWordIsAdjacent = afterEncodedWord;
            var currentCharacterIsWhitespace = char.IsWhiteSpace(character);
            var hasWordSeparator = previousWordIsAdjacent && currentCharacterIsWhitespace;
            if (hasWordSeparator) {
                var next = index;
                while (next < value.Length) {
                    var nextCharacterIsWhitespace = char.IsWhiteSpace(value[next]);
                    if (nextCharacterIsWhitespace == false) {
                        break;
                    }
                    next++;
                }
                var hasFollowingMarkerLength = next + 1 < value.Length;
                var hasEncodedWordMarker = hasFollowingMarkerLength &&
                                           value[next] == '=' &&
                                           value[next + 1] == '?';
                var isAdjacentEncodedWord = hasEncodedWordMarker && FindWord(value, next) is not null;
                if (isAdjacentEncodedWord) {
                    index = next;
                    afterEncodedWord = false;
                    continue;
                }
                builder.Append(value, index, next - index);
                index = next;
                afterEncodedWord = false;
                continue;
            }
            if (character == '"') {
                var end = FindQuoteEnd(value, index);
                builder.Append(value, index, end - index);
                index = end;
                afterEncodedWord = false;
                continue;
            }
            if (character == '<') {
                var end = value.IndexOf('>', index);
                if (end < 0) {
                    builder.Append(value, index, value.Length - index);
                    break;
                }
                builder.Append(value, index, end - index + 1);
                index = end + 1;
                afterEncodedWord = false;
                continue;
            }
            var hasCurrentMarkerLength = index + 1 < value.Length;
            var startsEncodedWord = character == '=' && hasCurrentMarkerLength && value[index + 1] == '?';
            if (startsEncodedWord) {
                var word = FindWord(value, index);
                if (word is not null) {
                    builder.Append(DecodeWord(word, diagnostics, offset));
                    index = word.End;
                    afterEncodedWord = true;
                    continue;
                }
            }
            builder.Append(character);
            index++;
            afterEncodedWord = false;
        }
        return builder.ToString();
    }

    private static int FindQuoteEnd(string value, int start) {
        var index = start + 1;
        while (index < value.Length) {
            if (value[index] == '\\') {
                index += 2;
                continue;
            }
            if (value[index] == '"') {
                return index + 1;
            }
            index++;
        }
        return value.Length;
    }

    /// <summary>
    /// Returns whether the specified text is whitespace only.
    /// </summary>
    /// <param name="text">
    /// The text to examine.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the specified text is whitespace only, or empty.
    /// </returns>
    private static bool IsWhiteSpace(string text) {
        for (var i = 0; i < text.Length; i++) {
            if (char.IsWhiteSpace(text[i]) == false) {
                return false;
            }
        }
        return true;
    }

    private static EncodedWord FindWord(string value, int start) {
        var charsetEnd = value.IndexOf('?', start + 2);
        if (charsetEnd < 0) {
            return null;
        }
        var encodingEnd = value.IndexOf('?', charsetEnd + 1);
        if (encodingEnd < 0) {
            return null;
        }
        var textEnd = value.IndexOf("?=", encodingEnd + 1, StringComparison.Ordinal);
        if (textEnd < 0) {
            return null;
        }
        var text = value.Substring(encodingEnd + 1, textEnd - encodingEnd - 1);
        return new EncodedWord {
            Charset = value.Substring(start + 2, charsetEnd - start - 2),
            Encoding = value.Substring(charsetEnd + 1, encodingEnd - charsetEnd - 1),
            Text = text,
            Start = start,
            End = textEnd + 2,
            Original = value.Substring(start, textEnd + 2 - start),
        };
    }

    private static string DecodeWord(EncodedWord word, List<MBoxDiagnostic> diagnostics, long offset) {
        if (word.End - word.Start > MaxEncodedWordLength) {
            diagnostics?.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.MalformedEncoding,
                offset,
                "The encoded word is longer than 75 characters and was kept as it is."));
            return word.Original;
        }
        var encoding = word.Encoding;
        var isB = encoding.StartsWith("B", StringComparison.OrdinalIgnoreCase);
        var isQ = encoding.StartsWith("Q", StringComparison.OrdinalIgnoreCase);
        if (isB == false && isQ == false) {
            diagnostics?.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.MalformedEncoding,
                offset,
                $"The encoded word uses the unknown encoding [{encoding}] and was kept as it is."));
            return word.Original;
        }
        byte[] bytes;
        if (isB) {
            try {
                bytes = Convert.FromBase64String(word.Text);
            }
            catch (FormatException) {
                diagnostics?.Add(new MBoxDiagnostic(
                    MBoxDiagnosticCode.MalformedEncoding,
                    offset,
                    "The encoded word is not valid base64 and was kept as it is."));
                return word.Original;
            }
        }
        else {
            bytes = DecodeQuotedPrintable(word.Text);
        }
        var charset = MBoxCharset.GetEncoding(word.Charset);
        if (charset is null) {
            diagnostics?.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.UnsupportedCharset,
                offset,
                $"The encoded word uses the unsupported character set [{word.Charset}] and was kept as it is."));
            return word.Original;
        }
        try {
            return charset.GetString(bytes);
        }
        catch (DecoderFallbackException) {
            diagnostics?.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.MalformedEncoding,
                offset,
                $"The encoded word is not valid [{word.Charset}] text and was kept as it is."));
            return word.Original;
        }
    }

    private static byte[] DecodeQuotedPrintable(string text) {
        var bytes = new List<byte>(text.Length);
        for (var i = 0; i < text.Length; i++) {
            var character = text[i];
            if (character == '_') {
                bytes.Add((byte)' ');
                continue;
            }
            if (character == '=' && i + 2 < text.Length) {
                var hex = text.Substring(i + 1, 2);
                if (byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)) {
                    bytes.Add(value);
                    i += 2;
                    continue;
                }
            }
            bytes.Add((byte)character);
        }
        return bytes.ToArray();
    }

    private sealed class EncodedWord {
        internal string Charset { get; init; }
        internal string Encoding { get; init; }
        internal string Text { get; init; }
        internal int Start { get; init; }
        internal int End { get; init; }
        internal string Original { get; init; }
    }
}
