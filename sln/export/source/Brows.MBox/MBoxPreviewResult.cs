using Brows.MBox.Format;
using System;
using System.Collections.Generic;

namespace Brows;

/// <summary>
/// Holds the bounded preview of a message, as it is shown in the detail of a mailbox.
/// </summary>
internal sealed record MBoxPreviewResult {
    /// <summary>
    /// Gets the selected text of the message.
    /// </summary>
    public string Text {
        get;
        init;
    }

    /// <summary>
    /// Gets the selected HTML source of the message.
    /// </summary>
    public string Html {
        get;
        init;
    }

    /// <summary>
    /// Gets how completely the selected text was read.
    /// </summary>
    public MBoxBodyState State {
        get;
        init;
    }

    /// <summary>
    /// Gets the problems that were found while reading the message.
    /// </summary>
    public IReadOnlyList<MBoxDiagnostic> Diagnostics {
        get;
        init;
    }

    public IReadOnlyList<MBoxPreviewAttachment> Attachments {
        get;
        init;
    } = Array.Empty<MBoxPreviewAttachment>();

    /// <summary>
    /// Gets the number of bytes of the preview text and the attachment names, which is used to
    /// bound the cache.
    /// </summary>
    public long Size {
        get {
            var length = (long)(Text?.Length ?? 0) + (Html?.Length ?? 0);
            foreach (var attachment in Attachments) {
                length += (attachment.Name?.Length ?? 0) + (attachment.ContentType?.Length ?? 0);
            }
            return length * sizeof(char);
        }
    }

    /// <summary>
    /// Gets the preview that holds nothing.
    /// </summary>
    public static MBoxPreviewResult Empty {
        get => new() {
            State = MBoxBodyState.Unavailable,
            Diagnostics = Array.Empty<MBoxDiagnostic>(),
        };
    }
}
