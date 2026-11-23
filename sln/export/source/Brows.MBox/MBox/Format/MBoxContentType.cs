using System;
using System.Collections.Generic;

namespace Brows.MBox.Format;

/// <summary>
/// Describes the content type of a message or a MIME part.
/// </summary>
internal sealed record MBoxContentType {
    /// <summary>
    /// Gets the type of the content, such as <c>text</c>.
    /// </summary>
    internal string Type { get; }

    /// <summary>
    /// Gets the subtype of the content, such as <c>plain</c>.
    /// </summary>
    internal string Subtype { get; }

    /// <summary>
    /// Gets the full content type, such as <c>text/plain</c>.
    /// </summary>
    internal string Full { get; }

    /// <summary>
    /// Gets the boundary parameter of a multipart content type, or <see langword="null"/> when there is none.
    /// </summary>
    internal string Boundary { get; }

    /// <summary>
    /// Gets the charset parameter, or <see langword="null"/> when there is none.
    /// </summary>
    internal string Charset { get; }

    /// <summary>
    /// Gets the parameters of the content type, keyed by parameter name.
    /// </summary>
    internal IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxContentType"/> class.
    /// </summary>
    /// <param name="type">
    /// The type of the content.
    /// </param>
    /// <param name="subtype">
    /// The subtype of the content.
    /// </param>
    /// <param name="full">
    /// The full content type.
    /// </param>
    /// <param name="boundary">
    /// The boundary parameter, or <see langword="null"/> when there is none.
    /// </param>
    /// <param name="charset">
    /// The charset parameter, or <see langword="null"/> when there is none.
    /// </param>
    /// <param name="parameters">
    /// The parameters of the content type.
    /// </param>
    internal MBoxContentType(string type,
                           string subtype,
                           string full,
                           string boundary,
                           string charset,
                           IReadOnlyDictionary<string, string> parameters) {
        Type = type ?? string.Empty;
        Subtype = subtype ?? string.Empty;
        Full = full ?? $"{Type}/{Subtype}";
        Boundary = boundary;
        Charset = charset;
        Parameters = MBoxCollection.Own(parameters, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns whether the content type is a multipart type.
    /// </summary>
    internal bool IsMultipart =>
        string.Equals(Type, "multipart", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns whether the content type is a text type.
    /// </summary>
    internal bool IsText =>
        string.Equals(Type, "text", StringComparison.OrdinalIgnoreCase);
}
