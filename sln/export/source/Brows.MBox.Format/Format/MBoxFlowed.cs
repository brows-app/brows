using System;
using System.Collections.Generic;
using System.Text;

namespace Brows.MBox.Format;

/// <summary>
/// Decodes text that uses the <c>format=flowed</c> parameter.
/// </summary>
/// <remarks>
/// The decoding follows RFC 3676. The quote marks of a line are counted and removed first, then a
/// single leading stuffing space is removed, and only then the line is classified as flowed or
/// fixed. A flowed line ends with a space, which is a soft line break when the line is joined with
/// the line that follows it.
/// </remarks>
internal static class MBoxFlowed {
    /// <summary>
    /// The signature separator line of RFC 3676.
    /// </summary>
    private const string Signature = "-- ";

    /// <summary>
    /// Decodes flowed text.
    /// </summary>
    /// <param name="text">
    /// The flowed text.
    /// </param>
    /// <param name="delSp">
    /// Whether the <c>delsp=yes</c> parameter was declared.
    /// </param>
    /// <returns>
    /// The decoded text.
    /// </returns>
    public static string Decode(string text, bool delSp) {
        if (string.IsNullOrEmpty(text)) {
            return text ?? string.Empty;
        }
        var lines = SplitLines(text);
        var endsWithLineBreak = text[^1] == '\n';
        var builder = new StringBuilder(text.Length);
        var pending = new StringBuilder();
        var pendingDepth = 0;
        var hasPending = false;
        for (var i = 0; i < lines.Count; i++) {
            var raw = lines[i];
            if (raw == Signature) {
                /*
                 * An unquoted signature separator is a hard boundary, and it is never soft-flowed.
                 */
                Flush(builder, pending, ref pendingDepth, ref hasPending);
                builder.Append(raw);
                builder.Append('\n');
                continue;
            }
            var depth = QuoteDepth(raw);
            var content = RemoveQuotes(raw);
            if (content.StartsWith(" ", StringComparison.Ordinal)) {
                /*
                 * The first space of the remaining content is the stuffing space.
                 */
                content = content[1..];
            }
            if (content == Signature) {
                /*
                 * A quoted signature separator is a hard boundary as well.
                 */
                Flush(builder, pending, ref pendingDepth, ref hasPending);
                builder.Append('>', depth);
                builder.Append(content);
                builder.Append('\n');
                continue;
            }
            var isFlowed = content.Length > 0 && content[^1] == ' ';
            if (isFlowed && delSp) {
                /*
                 * Only the marker space of the soft line break is deleted. Any other trailing
                 * space belongs to the content.
                 */
                content = content[..^1];
            }
            if (isFlowed) {
                if (hasPending && pendingDepth == depth) {
                    pending.Append(content);
                    continue;
                }
                /*
                 * A change of quote depth ends the paragraph, so the pending lines are written
                 * before the new paragraph is started.
                 */
                Flush(builder, pending, ref pendingDepth, ref hasPending);
                pendingDepth = depth;
                pending.Append(content);
                hasPending = true;
                continue;
            }
            if (hasPending && pendingDepth == depth && content.Length > 0) {
                /*
                 * The fixed line ends the paragraph it belongs to, so it is joined with the
                 * flowed lines that precede it.
                 */
                pending.Append(content);
                Flush(builder, pending, ref pendingDepth, ref hasPending);
                continue;
            }
            /*
             * A fixed line that does not end a paragraph is kept on its own line, and an empty
             * line is always kept so that the blank lines of the body survive.
             */
            Flush(builder, pending, ref pendingDepth, ref hasPending);
            builder.Append('>', depth);
            builder.Append(content);
            builder.Append('\n');
        }
        Flush(builder, pending, ref pendingDepth, ref hasPending);
        var result = builder.ToString();
        if (endsWithLineBreak == false && result.Length > 0 && result[^1] == '\n') {
            result = result[..^1];
        }
        return result;
    }

    /// <summary>
    /// Writes the pending paragraph and clears it.
    /// </summary>
    /// <param name="builder">
    /// The builder that receives the paragraph.
    /// </param>
    /// <param name="pending">
    /// The content of the pending paragraph.
    /// </param>
    /// <param name="pendingDepth">
    /// The quote depth of the pending paragraph.
    /// </param>
    /// <param name="hasPending">
    /// Whether a paragraph is pending.
    /// </param>
    private static void Flush(StringBuilder builder,
                               StringBuilder pending,
                               ref int pendingDepth,
                               ref bool hasPending) {
        if (hasPending == false) {
            return;
        }
        /*
         * The quote depth of the paragraph is restored before its content.
         */
        builder.Append('>', pendingDepth);
        builder.Append(pending);
        builder.Append('\n');
        pending.Clear();
        pendingDepth = 0;
        hasPending = false;
    }

    /// <summary>
    /// Returns the quote depth of a line, which is the number of its leading quote marks.
    /// </summary>
    /// <param name="line">
    /// The line to examine.
    /// </param>
    /// <returns>
    /// The quote depth of the line.
    /// </returns>
    private static int QuoteDepth(string line) {
        var depth = 0;
        while (depth < line.Length && line[depth] == '>') {
            depth++;
        }
        return depth;
    }

    /// <summary>
    /// Removes the leading quote marks of a line.
    /// </summary>
    /// <param name="line">
    /// The line to examine.
    /// </param>
    /// <returns>
    /// The line without its leading quote marks.
    /// </returns>
    private static string RemoveQuotes(string line) {
        var depth = QuoteDepth(line);
        if (depth == 0) {
            return line;
        }
        return line[depth..];
    }

    /// <summary>
    /// Splits text into its lines, without their terminators.
    /// </summary>
    /// <param name="text">
    /// The text to split.
    /// </param>
    /// <returns>
    /// The lines of the text.
    /// </returns>
    private static List<string> SplitLines(string text) {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++) {
            if (text[i] == '\n') {
                var end = i;
                if (end > start && text[end - 1] == '\r') {
                    end--;
                }
                lines.Add(text[start..end]);
                start = i + 1;
            }
        }
        if (start < text.Length) {
            var end = text.Length;
            if (end > start && text[end - 1] == '\r') {
                end--;
            }
            lines.Add(text[start..end]);
        }
        return lines;
    }
}
