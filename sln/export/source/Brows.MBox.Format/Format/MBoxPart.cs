using System;
using System.Collections.Generic;

namespace Brows.MBox.Format;

/// <summary>
/// Describes a single MIME part of a message.
/// </summary>
/// <remarks>
/// A part never exposes the raw bytes of a binary body. The exact bytes of a message are
/// streamed separately by the extraction operation.
/// </remarks>
public sealed record MBoxPart {
    /// <summary>
    /// Gets the content type of the part.
    /// </summary>
    public MBoxContentType ContentType { get; }

    /// <summary>
    /// Gets the declared character set of the part, or <see langword="null"/> when there is none.
    /// </summary>
    public string Charset { get; }

    /// <summary>
    /// Gets the content transfer encoding of the part, or <see langword="null"/> when there is none.
    /// </summary>
    public string TransferEncoding { get; }

    /// <summary>
    /// Gets the content disposition of the part, or <see langword="null"/> when there is none.
    /// </summary>
    public string Disposition { get; }

    /// <summary>
    /// Gets the file name of the part, or <see langword="null"/> when there is none.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the content identifier of the part, or <see langword="null"/> when there is none.
    /// </summary>
    public string ContentId { get; }

    /// <summary>
    /// Gets the number of raw bytes of the part, or <see langword="null"/> when it is not known.
    /// </summary>
    public long? RawLength { get; }

    /// <summary>
    /// Gets the number of decoded bytes of the part, or <see langword="null"/> when it is not known.
    /// </summary>
    public long? DecodedLength { get; }

    /// <summary>
    /// Gets the decoded text of the part, or <see langword="null"/> when the part was not decoded.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets how completely the part was read.
    /// </summary>
    public MBoxBodyState State { get; }

    /// <summary>
    /// Gets the problems found while reading the part.
    /// </summary>
    public IReadOnlyList<MBoxDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets whether the part is excluded from the display body because it is an attachment.
    /// </summary>
    public bool IsAttachmentPart { get; init; }

    /// <summary>
    /// Gets whether the part holds HTML source.
    /// </summary>
    public bool IsHtml =>
        string.Equals(ContentType.Subtype, "html", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether the part holds plain text.
    /// </summary>
    public bool IsText =>
        ContentType.IsText && IsHtml == false;

    /// <summary>
    /// Gets whether the part holds text that can be displayed.
    /// </summary>
    public bool HasDisplayBody =>
        IsText || IsHtml;

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxPart"/> class.
    /// </summary>
    /// <param name="contentType">
    /// The content type of the part.
    /// </param>
    /// <param name="charset">
    /// The declared character set of the part.
    /// </param>
    /// <param name="transferEncoding">
    /// The content transfer encoding of the part.
    /// </param>
    /// <param name="disposition">
    /// The content disposition of the part.
    /// </param>
    /// <param name="fileName">
    /// The file name of the part.
    /// </param>
    /// <param name="contentId">
    /// The content identifier of the part.
    /// </param>
    /// <param name="rawLength">
    /// The number of raw bytes of the part.
    /// </param>
    /// <param name="decodedLength">
    /// The number of decoded bytes of the part.
    /// </param>
    /// <param name="text">
    /// The decoded text of the part.
    /// </param>
    /// <param name="state">
    /// How completely the part was read.
    /// </param>
    /// <param name="diagnostics">
    /// The problems found while reading the part.
    /// </param>
    public MBoxPart(MBoxContentType contentType,
                    string charset,
                    string transferEncoding,
                    string disposition,
                    string fileName,
                    string contentId,
                    long? rawLength,
                    long? decodedLength,
                    string text,
                    MBoxBodyState state,
                    IReadOnlyList<MBoxDiagnostic> diagnostics) {
        ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        Charset = charset;
        TransferEncoding = transferEncoding;
        Disposition = disposition;
        FileName = fileName;
        ContentId = contentId;
        RawLength = rawLength;
        DecodedLength = decodedLength;
        Text = text;
        State = state;
        Diagnostics = MBoxCollection.Own(diagnostics);
    }
}
