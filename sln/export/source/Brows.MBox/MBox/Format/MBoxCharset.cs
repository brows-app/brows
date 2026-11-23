using System;
using System.Collections.Generic;
using System.Text;

namespace Brows.MBox.Format;

/// <summary>
/// Decodes text using the character set declared by a message.
/// </summary>
internal static class MBoxCharset {
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase) {
        ["utf8"] = "utf-8",
        ["utf-8"] = "utf-8",
        ["us-ascii"] = "ascii",
        ["ascii"] = "ascii",
        ["iso-8859-1"] = "latin-1",
        ["iso8859-1"] = "latin-1",
        ["latin1"] = "latin-1",
        ["latin-1"] = "latin-1",
        ["windows-1252"] = "windows-1252",
        ["cp1252"] = "windows-1252",
        ["iso-8859-15"] = "iso-8859-15",
    };

    private static Encoding Strict(Encoding encoding) {
        var strict = (Encoding)encoding.Clone();
        strict.DecoderFallback = DecoderFallback.ExceptionFallback;
        return strict;
    }

    /// <summary>
    /// Returns the encoding for the specified declared character set, or <see langword="null"/>
    /// when the character set is not supported.
    /// </summary>
    /// <param name="charset">
    /// The declared character set. May be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The encoding for the specified declared character set, or <see langword="null"/>.
    /// </returns>
    internal static Encoding GetEncoding(string charset) {
        if (string.IsNullOrWhiteSpace(charset)) {
            return null;
        }
        var name = charset.Trim().Trim('"');
        if (name.Length == 0) {
            return null;
        }
        if (Aliases.TryGetValue(name, out var alias)) {
            name = alias;
        }
        if (name.Equals("utf-8", StringComparison.OrdinalIgnoreCase)) {
            return Strict(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        }
        if (name.Equals("ascii", StringComparison.OrdinalIgnoreCase)) {
            return Strict(new ASCIIEncoding());
        }
        if (name.Equals("latin-1", StringComparison.OrdinalIgnoreCase)) {
            return Strict(Encoding.Latin1);
        }
        try {
            var encoding = CodePagesEncodingProvider.Instance.GetEncoding(name);
            return encoding is null ? null : Strict(encoding);
        }
        catch (ArgumentException) {
            return null;
        }
        catch (NotSupportedException) {
            return null;
        }
    }

    /// <summary>
    /// Returns whether the specified declared character set is a known character set.
    /// </summary>
    /// <param name="charset">
    /// The declared character set. May be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the specified declared character set is known.
    /// </returns>
    internal static bool IsKnown(string charset) {
        return GetEncoding(charset) is not null;
    }
}
