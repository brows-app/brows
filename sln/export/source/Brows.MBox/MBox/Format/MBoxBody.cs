using System;
using System.Collections.Generic;

namespace Brows.MBox.Format;

/// <summary>
/// Describes the body of a message.
/// </summary>
internal sealed record MBoxBody {
    /// <summary>
    /// Gets the content type of the body.
    /// </summary>
    internal MBoxContentType ContentType { get; }

    /// <summary>
    /// Gets the parts of the body, in the order they were visited.
    /// </summary>
    internal IReadOnlyList<MBoxPart> Parts { get; }

    /// <summary>
    /// Gets the selected plain text of the body, or <see langword="null"/> when there is none.
    /// </summary>
    internal string Text { get; }

    /// <summary>
    /// Gets the selected HTML source of the body, or <see langword="null"/> when there is none.
    /// </summary>
    internal string Html { get; }

    /// <summary>
    /// Gets how completely the body was read.
    /// </summary>
    internal MBoxBodyState State { get; }

    /// <summary>
    /// Gets the number of raw bytes of the body, or <see langword="null"/> when it is not known.
    /// </summary>
    internal long? Length { get; }

    /// <summary>
    /// Gets the problems found while reading the body.
    /// </summary>
    internal IReadOnlyList<MBoxDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxBody"/> class.
    /// </summary>
    /// <param name="contentType">
    /// The content type of the body.
    /// </param>
    /// <param name="parts">
    /// The parts of the body.
    /// </param>
    /// <param name="text">
    /// The selected plain text of the body.
    /// </param>
    /// <param name="html">
    /// The selected HTML source of the body.
    /// </param>
    /// <param name="state">
    /// How completely the body was read.
    /// </param>
    /// <param name="length">
    /// The number of raw bytes of the body.
    /// </param>
    /// <param name="diagnostics">
    /// The problems found while reading the body.
    /// </param>
    internal MBoxBody(MBoxContentType contentType,
                    IReadOnlyList<MBoxPart> parts,
                    string text,
                    string html,
                    MBoxBodyState state,
                    long? length,
                    IReadOnlyList<MBoxDiagnostic> diagnostics) {
        ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        Parts = MBoxCollection.Own(parts);
        Text = text;
        Html = html;
        State = state;
        Length = length;
        Diagnostics = MBoxCollection.Own(diagnostics);
    }
}
