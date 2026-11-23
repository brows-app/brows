using System;
using System.Collections.Generic;
using System.Text;

namespace Brows.MBox.Format;

/// <summary>
/// Parses content type fields, including their parameters.
/// </summary>
internal static class MBoxContentTypeParser {
    /// <summary>
    /// Parses a content type field value.
    /// </summary>
    /// <param name="value">
    /// The field value.
    /// </param>
    /// <param name="diagnostics">
    /// The list that receives the problems found while parsing.
    /// </param>
    /// <param name="offset">
    /// The absolute offset used to report diagnostics.
    /// </param>
    /// <returns>
    /// The parsed content type.
    /// </returns>
    internal static MBoxContentType Parse(string value,
                                        List<MBoxDiagnostic> diagnostics,
                                        long offset) {
        if (string.IsNullOrWhiteSpace(value)) {
            return MBoxContentTypes.TextPlain;
        }
        var parts = SplitParameters(value);
        var media = parts[0].Trim();
        var slash = media.IndexOf('/');
        string type;
        string subtype;
        if (slash < 0) {
            type = media;
            subtype = string.Empty;
            diagnostics?.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.MalformedMime,
                offset,
                $"The content type [{media}] has no subtype and was read as malformed."));
        }
        else {
            type = media[..slash].Trim();
            subtype = media[(slash + 1)..].Trim();
        }
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < parts.Count; i++) {
            var parameter = parts[i];
            var equals = parameter.IndexOf('=');
            if (equals < 0) {
                diagnostics?.Add(new MBoxDiagnostic(
                    MBoxDiagnosticCode.MalformedMime,
                    offset,
                    "A content type parameter has no value and was ignored."));
                continue;
            }
            var name = parameter[..equals].Trim();
            if (name.Length == 0) {
                continue;
            }
            parameters[name] = Unquote(parameter[(equals + 1)..].Trim());
        }
        parameters.TryGetValue("boundary", out var boundary);
        parameters.TryGetValue("charset", out var charset);
        var full = subtype.Length == 0 ? type : $"{type}/{subtype}";
        return new MBoxContentType(type, subtype, full, boundary, charset, parameters);
    }

    /// <summary>
    /// Splits a content type field value into its media type and its parameters.
    /// </summary>
    /// <param name="value">
    /// The field value.
    /// </param>
    /// <returns>
    /// The media type and the parameters.
    /// </returns>
    internal static List<string> SplitParameters(string value) {
        var parts = new List<string>();
        var builder = new StringBuilder();
        var quoted = false;
        var escaped = false;
        for (var i = 0; i < value.Length; i++) {
            var character = value[i];
            if (escaped) {
                builder.Append(character);
                escaped = false;
                continue;
            }
            if (character == '\\' && quoted) {
                builder.Append(character);
                escaped = true;
                continue;
            }
            if (character == '"') {
                quoted = !quoted;
                builder.Append(character);
                continue;
            }
            if (character == ';' && quoted == false) {
                parts.Add(builder.ToString());
                builder.Clear();
                continue;
            }
            builder.Append(character);
        }
        parts.Add(builder.ToString());
        return parts;
    }

    private static string Unquote(string value) {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') {
            var inner = value[1..^1];
            var builder = new StringBuilder(inner.Length);
            var escaped = false;
            for (var i = 0; i < inner.Length; i++) {
                var character = inner[i];
                if (escaped) {
                    builder.Append(character);
                    escaped = false;
                    continue;
                }
                if (character == '\\') {
                    escaped = true;
                    continue;
                }
                builder.Append(character);
            }
            return builder.ToString();
        }
        return value;
    }
}
