using Brows.MBox.Format;
using System;

namespace Brows;

/// <summary>
/// Holds the metadata of a message, as it is shown in the list of a mailbox.
/// </summary>
internal sealed record MBoxMetadata {
    /// <summary>
    /// Gets the subject of the message.
    /// </summary>
    public string Subject {
        get;
        init;
    }

    /// <summary>
    /// Gets the display name of the sender of the message.
    /// </summary>
    public string From {
        get;
        init;
    }

    /// <summary>
    /// Gets the display names of the recipients of the message.
    /// </summary>
    public string To {
        get;
        init;
    }

    /// <summary>
    /// Gets the date of the message, or <see langword="null"/> when the message has none.
    /// </summary>
    public DateTimeOffset? Date {
        get;
        init;
    }

    /// <summary>
    /// Gets the message identifier of the message.
    /// </summary>
    public string MessageId {
        get;
        init;
    }

    /// <summary>
    /// Gets the labels of the message, as they are stored in the mail folder of Gmail.
    /// </summary>
    public string Labels {
        get;
        init;
    }

    /// <summary>
    /// Gets the identifier of the thread of the message.
    /// </summary>
    public string ThreadId {
        get;
        init;
    }

    /// <summary>
    /// Gets the number of bytes that the stored record of the message occupies.
    /// </summary>
    public long Size {
        get;
        init;
    }

    /// <summary>
    /// Gets the number of bytes of the text of the message that were truncated.
    /// </summary>
    public bool Truncated {
        get;
        init;
    }
}
