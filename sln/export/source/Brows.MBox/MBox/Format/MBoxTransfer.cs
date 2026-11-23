using System;
using System.Collections.Generic;
using System.Text;

namespace Brows.MBox.Format;

/// <summary>
/// Decodes the content transfer encodings used by MIME parts.
/// </summary>
internal static class MBoxTransfer {
    /// <summary>
    /// Decodes base64 content.
    /// </summary>
    /// <param name="rawBytes">
    /// The encoded bytes.
    /// </param>
    /// <param name="diagnostics">
    /// The list that receives the problems found while decoding.
    /// </param>
    /// <param name="state">
    /// The state of the read.
    /// </param>
    /// <returns>
    /// The decoded bytes, or <see langword="null"/> when the content cannot be decoded.
    /// </returns>
    internal static byte[] DecodeBase64(byte[] rawBytes,
                                      List<MBoxDiagnostic> diagnostics,
                                      MBoxBodyReader.ReadState state) {
        var bytes = new List<byte>((int)((long)rawBytes.Length * 3 / 4 + 3));
        var accumulator = 0;
        var bits = 0;
        var invalid = false;
        var symbols = 0;
        var padding = 0;
        var isPadded = false;
        for (var i = 0; i < rawBytes.Length; i++) {
            var character = (char)rawBytes[i];
            var isWhitespace = character == '\r' ||
                               character == '\n' ||
                               character == ' ' ||
                               character == '\t';
            if (isWhitespace) {
                continue;
            }
            if (character == '=') {
                isPadded = true;
                padding++;
                if (padding > 2) {
                    invalid = true;
                }
                continue;
            }
            var value = Value(character);
            if (value < 0) {
                invalid = true;
                continue;
            }
            if (isPadded) {
                invalid = true;
                continue;
            }
            symbols++;
            accumulator = (accumulator << 6) | value;
            bits += 6;
            if (bits >= 8) {
                bits -= 8;
                bytes.Add((byte)((accumulator >> bits) & 0xFF));
            }
            if (bits == 0) {
                accumulator = 0;
            }
        }
        var remainder = symbols % 4;
        if (padding == 0) {
            if (remainder != 0) {
                invalid = true;
            }
        }
        else {
            var paddingAndRemainderDoNotMatch = (padding == 1 && remainder != 3) ||
                                                (padding == 2 && remainder != 2);
            if (paddingAndRemainderDoNotMatch) {
                invalid = true;
            }
        }
        var unusedBitsAreNonZero = (remainder == 2 && (accumulator & 0x0F) != 0) ||
                                   (remainder == 3 && (accumulator & 0x03) != 0);
        if (unusedBitsAreNonZero) {
            invalid = true;
        }
        if (invalid) {
            diagnostics.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.MalformedEncoding,
                state.Reader.Position,
                "The base64 content contains characters that are not part of the encoding."));
        }
        return bytes.ToArray();
    }

    /// <summary>
    /// Decodes quoted-printable content.
    /// </summary>
    /// <param name="rawBytes">
    /// The encoded bytes.
    /// </param>
    /// <param name="diagnostics">
    /// The list that receives the problems found while decoding.
    /// </param>
    /// <param name="state">
    /// The state of the read.
    /// </param>
    /// <param name="keepLineBreaks">
    /// <see langword="true"/> to keep each line break as it is stored; <see langword="false"/> to
    /// write each line break as a line feed.
    /// </param>
    /// <returns>
    /// The decoded bytes, or <see langword="null"/> when the content cannot be decoded.
    /// </returns>
    internal static byte[] DecodeQuotedPrintable(byte[] rawBytes,
                                               List<MBoxDiagnostic> diagnostics,
                                               MBoxBodyReader.ReadState state,
                                               bool keepLineBreaks = false) {
        var bytes = new List<byte>(rawBytes.Length);
        var invalid = false;
        for (var i = 0; i < rawBytes.Length; i++) {
            var character = (char)rawBytes[i];
            if (character == '=') {
                var hasSoftLineBreak = i + 1 < rawBytes.Length &&
                                       (rawBytes[i + 1] == (byte)'\n' || rawBytes[i + 1] == (byte)'\r');
                if (hasSoftLineBreak) {
                    /*
                     * A soft line break.
                     */
                    i++;
                    var hasCarriageReturnLineFeed = i < rawBytes.Length &&
                                                    rawBytes[i] == (byte)'\r' &&
                                                    i + 1 < rawBytes.Length &&
                                                    rawBytes[i + 1] == (byte)'\n';
                    if (hasCarriageReturnLineFeed) {
                        i++;
                    }
                    continue;
                }
                if (i + 2 < rawBytes.Length) {
                    var high = Hex((char)rawBytes[i + 1]);
                    var low = Hex((char)rawBytes[i + 2]);
                    var escapeIsValid = high >= 0 && low >= 0;
                    if (escapeIsValid) {
                        bytes.Add((byte)((high << 4) | low));
                        i += 2;
                        continue;
                    }
                }
                invalid = true;
                bytes.Add(rawBytes[i]);
                continue;
            }
            var lineBreakIsNormalized = character == '\r' && keepLineBreaks == false;
            if (lineBreakIsNormalized) {
                /*
                 * A carriage return followed by a line feed is a single line terminator.
                 */
                var hasLineFeed = i + 1 < rawBytes.Length && rawBytes[i + 1] == (byte)'\n';
                if (hasLineFeed) {
                    i++;
                }
                bytes.Add((byte)'\n');
                continue;
            }
            bytes.Add(rawBytes[i]);
        }
        if (invalid) {
            diagnostics.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.MalformedEncoding,
                state.Reader.Position,
                "The quoted-printable content contains a malformed escape sequence."));
        }
        return bytes.ToArray();
    }

    private static int Value(char character) {
        var isUppercaseLetter = character >= 'A' && character <= 'Z';
        if (isUppercaseLetter) {
            return character - 'A';
        }
        var isLowercaseLetter = character >= 'a' && character <= 'z';
        if (isLowercaseLetter) {
            return character - 'a' + 26;
        }
        var isDigit = character >= '0' && character <= '9';
        if (isDigit) {
            return character - '0' + 52;
        }
        if (character == '+') {
            return 62;
        }
        if (character == '/') {
            return 63;
        }
        return -1;
    }

    private static int Hex(char character) {
        var isDigit = character >= '0' && character <= '9';
        if (isDigit) {
            return character - '0';
        }
        var isUppercaseHexLetter = character >= 'A' && character <= 'F';
        if (isUppercaseHexLetter) {
            return character - 'A' + 10;
        }
        var isLowercaseHexLetter = character >= 'a' && character <= 'f';
        if (isLowercaseHexLetter) {
            return character - 'a' + 10;
        }
        return -1;
    }
}
