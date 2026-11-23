using System;

namespace Brows.MBox.Format;

/// <summary>
/// Holds the text that was selected for display.
/// </summary>
internal sealed class MBoxSelection {
    /// <summary>
    /// Gets the selected plain text.
    /// </summary>
    internal string Text { get; init; }

    /// <summary>
    /// Gets the selected HTML source.
    /// </summary>
    internal string Html { get; init; }

    /// <summary>
    /// Gets how completely the selected text was read.
    /// </summary>
    internal MBoxBodyState State { get; init; }

    /// <summary>
    /// Gets whether the selection is excluded from the display body because it is an
    /// attachment.
    /// </summary>
    internal bool IsAttachment { get; init; }

    /// <summary>
    /// Gets the content identifier of the selected part.
    /// </summary>
    internal string ContentId { get; init; }

    /// <summary>
    /// Gets whether the selection holds plain text.
    /// </summary>
    internal bool HasText =>
        Text is not null;

    /// <summary>
    /// Gets whether the selection holds HTML source.
    /// </summary>
    internal bool HasHtml =>
        Html is not null;

    /// <summary>
    /// Gets whether the selection holds text that can be displayed.
    /// </summary>
    internal bool HasDisplayBody =>
        HasText || HasHtml;

    /// <summary>
    /// Gets the selection that holds nothing.
    /// </summary>
    internal static MBoxSelection Empty {
        get => new() { State = MBoxBodyState.Unavailable };
    }

    /// <summary>
    /// Returns the selection of a single part.
    /// </summary>
    /// <param name="part">
    /// The selected part. May be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The selection of the part.
    /// </returns>
    internal static MBoxSelection Of(MBoxPart part) {
        if (part is null) {
            return Empty;
        }
        if (part.IsHtml) {
            return new MBoxSelection {
                Html = part.Text,
                State = part.State,
                IsAttachment = part.IsAttachmentPart,
                ContentId = part.ContentId,
            };
        }
        return new MBoxSelection {
            Text = part.Text,
            State = part.State,
            IsAttachment = part.IsAttachmentPart,
            ContentId = part.ContentId,
        };
    }

    internal static MBoxSelection WithEntityContext(MBoxSelection selection,
                                                   bool isAttachment,
                                                   string contentId) {
        if (selection is null) {
            return Empty;
        }
        return new MBoxSelection {
            Text = selection.Text,
            Html = selection.Html,
            State = selection.State,
            IsAttachment = selection.IsAttachment || isAttachment,
            ContentId = contentId ?? selection.ContentId,
        };
    }
}
