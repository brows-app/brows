using Brows.Entries;
using Brows.MBox.Format;
using System;
using System.Globalization;

namespace Brows;

/// <summary>
/// Holds the message of a mailbox, as it is shown in the list of a mailbox.
/// </summary>
internal sealed class MBoxEntry : Entry<MBoxProvider> {
    /// <summary>
    /// Gets the metadata of the message.
    /// </summary>
    public MBoxMetadata Metadata {
        get;
    }

    /// <summary>
    /// Gets the record that locates the message in the mailbox.
    /// </summary>
    public MBoxRecord Record {
        get;
    }

    /// <summary>
    /// Gets the session that holds the mailbox of the message.
    /// </summary>
    public MBoxSession Session {
        get;
    }

    /// <summary>
    /// Gets the identifier of the generation of the data that the message belongs to.
    /// </summary>
    public string GenerationId =>
        Session.GenerationId;

    /// <summary>
    /// Gets the size of the stored record of the message, in bytes.
    /// </summary>
    public long Size =>
        Record.Length;

    /// <summary>
    /// Gets the subject of the message, or a localized placeholder when the message has none.
    /// </summary>
    public string Subject {
        get {
            var subject = Metadata.Subject;
            if (string.IsNullOrWhiteSpace(subject)) {
                return MBoxText.Value(Provider.Translation, "MBoxEntry_NoSubject");
            }
            return subject;
        }
    }

    /// <summary>
    /// Gets the date of the message, or <see langword="null"/> when the message has none.
    /// </summary>
    public DateTimeOffset? Date =>
        Metadata.Date;

    /// <summary>
    /// Gets the display name of the sender of the message.
    /// </summary>
    public string From =>
        Metadata.From;

    /// <summary>
    /// Gets the display names of the recipients of the message.
    /// </summary>
    public string To =>
        Metadata.To;

    /// <summary>
    /// Gets the message identifier of the message.
    /// </summary>
    public string MessageId =>
        Metadata.MessageId;

    /// <summary>
    /// Gets the labels of the message.
    /// </summary>
    public string Labels =>
        Metadata.Labels;

    /// <summary>
    /// Gets the identifier of the thread of the message.
    /// </summary>
    public string ThreadId =>
        Metadata.ThreadId;

    /// <summary>
    /// Gets the name of the message, which is its subject.
    /// </summary>
    public override string Name =>
        Subject;

    /// <summary>
    /// Gets the identifier of the message, which is stable within a generation of the mailbox.
    /// </summary>
    public override string ID =>
        $"{Provider.ID}|{GenerationId}|{Record.Offset.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxEntry"/> class.
    /// </summary>
    /// <param name="provider">
    /// The provider that shows the mailbox of the message.
    /// </param>
    /// <param name="session">
    /// The session that holds the mailbox of the message.
    /// </param>
    /// <param name="record">
    /// The record that locates the message in the mailbox.
    /// </param>
    /// <param name="metadata">
    /// The metadata of the message.
    /// </param>
    public MBoxEntry(MBoxProvider provider,
                     MBoxSession session,
                     MBoxRecord record,
                     MBoxMetadata metadata)
    : base(provider) {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Record = record ?? throw new ArgumentNullException(nameof(record));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }
}
