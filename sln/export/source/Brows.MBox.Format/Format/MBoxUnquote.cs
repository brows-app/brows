using System;

namespace Brows.MBox.Format;

/// <summary>
/// Removes the quoting that a mailbox applies to the body lines of a message.
/// </summary>
internal static class MBoxUnquote {
    /// <summary>
    /// The number of bytes of the <c>From </c> prefix that a quoted line follows with.
    /// </summary>
    private const int FromLength = 5;

    /// <summary>
    /// Returns whether the specified line is quoted in the specified dialect.
    /// </summary>
    /// <param name="line">
    /// The line to examine, without its terminator.
    /// </param>
    /// <param name="format">
    /// The mailbox storage dialect.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the specified line is quoted in the specified dialect.
    /// </returns>
    public static bool IsQuoted(string line, MBoxFormat format) {
        if (string.IsNullOrEmpty(line)) {
            return false;
        }
        var depth = QuoteDepth(line);
        if (depth == 0) {
            return false;
        }
        return IsQuoted(depth, FollowsWithFrom(line, depth), format);
    }

    /// <summary>
    /// Returns whether the specified line is quoted in the specified dialect.
    /// </summary>
    /// <param name="line">
    /// The line to examine, without its terminator.
    /// </param>
    /// <param name="format">
    /// The mailbox storage dialect.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the specified line is quoted in the specified dialect.
    /// </returns>
    public static bool IsQuoted(ReadOnlySpan<byte> line, MBoxFormat format) {
        if (line.Length == 0) {
            return false;
        }
        var depth = QuoteDepth(line);
        if (depth == 0) {
            return false;
        }
        return IsQuoted(depth, FollowsWithFrom(line, depth), format);
    }

    /// <summary>
    /// Returns whether a line with the specified number of leading quote marks, that is followed
    /// by the <c>From </c> prefix or not, is quoted in the specified dialect.
    /// </summary>
    /// <param name="depth">
    /// The number of quote marks the line starts with.
    /// </param>
    /// <param name="followsWithFrom">
    /// Whether the <c>From </c> prefix follows the quote marks of the line.
    /// </param>
    /// <param name="format">
    /// The mailbox storage dialect.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when such a line is quoted in the specified dialect.
    /// </returns>
    public static bool IsQuoted(int depth, bool followsWithFrom, MBoxFormat format) {
        if (depth == 0 || followsWithFrom == false) {
            return false;
        }
        /*
         * The mboxrd dialect quotes every line that matches "^>+From ", so any number of quote
         * marks is accepted.
         */
        if (format == MBoxFormat.Mboxrd) {
            return true;
        }
        /*
         * The mboxo dialect quotes only a line that matches exactly ">From ", so a line with
         * more than one quote mark is left alone.
         */
        return depth == 1;
    }

    /// <summary>
    /// Removes the quoting of a line.
    /// </summary>
    /// <param name="line">
    /// The line to unquote, without its terminator.
    /// </param>
    /// <param name="format">
    /// The mailbox storage dialect.
    /// </param>
    /// <returns>
    /// The line without its quoting.
    /// </returns>
    public static string Unquote(string line, MBoxFormat format) {
        if (IsQuoted(line, format) == false) {
            return line;
        }
        /*
         * Exactly one quote mark is removed, whatever the number of quote marks of the line is.
         */
        return line[1..];
    }

    /// <summary>
    /// Returns the number of quote marks the specified line starts with.
    /// </summary>
    /// <param name="line">
    /// The line to examine.
    /// </param>
    /// <returns>
    /// The number of leading quote marks of the line.
    /// </returns>
    private static int QuoteDepth(string line) {
        var depth = 0;
        while (depth < line.Length && line[depth] == '>') {
            depth++;
        }
        return depth;
    }

    /// <summary>
    /// Returns the number of quote marks the specified line starts with.
    /// </summary>
    /// <param name="line">
    /// The line to examine.
    /// </param>
    /// <returns>
    /// The number of leading quote marks of the line.
    /// </returns>
    private static int QuoteDepth(ReadOnlySpan<byte> line) {
        var depth = 0;
        while (depth < line.Length && line[depth] == (byte)'>') {
            depth++;
        }
        return depth;
    }

    /// <summary>
    /// Returns whether the <c>From </c> prefix follows the quote marks of the specified line.
    /// </summary>
    /// <param name="line">
    /// The line to examine.
    /// </param>
    /// <param name="offset">
    /// The offset of the quote marks of the line.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the prefix follows the quote marks of the line.
    /// </returns>
    private static bool FollowsWithFrom(string line, int offset) {
        if (line.Length < offset + FromLength) {
            return false;
        }
        return line[offset] == 'F' &&
               line[offset + 1] == 'r' &&
               line[offset + 2] == 'o' &&
               line[offset + 3] == 'm' &&
               line[offset + 4] == ' ';
    }

    /// <summary>
    /// Returns whether the <c>From </c> prefix follows the quote marks of the specified line.
    /// </summary>
    /// <param name="line">
    /// The line to examine.
    /// </param>
    /// <param name="offset">
    /// The offset of the quote marks of the line.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the prefix follows the quote marks of the line.
    /// </returns>
    private static bool FollowsWithFrom(ReadOnlySpan<byte> line, int offset) {
        if (line.Length < offset + FromLength) {
            return false;
        }
        return line[offset] == (byte)'F' &&
               line[offset + 1] == (byte)'r' &&
               line[offset + 2] == (byte)'o' &&
               line[offset + 3] == (byte)'m' &&
               line[offset + 4] == (byte)' ';
    }
}
