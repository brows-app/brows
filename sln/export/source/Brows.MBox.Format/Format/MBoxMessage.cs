using System;
using System.Collections.Generic;

namespace Brows.MBox.Format;

/// <summary>
/// Describes a message read from a mailbox.
/// </summary>
public sealed record MBoxMessage {
    /// <summary>
    /// Gets the location of the message inside the mailbox.
    /// </summary>
    public MBoxRecord Record { get; }

    /// <summary>
    /// Gets the header fields of the message.
    /// </summary>
    public MBoxHeaders Headers { get; }

    /// <summary>
    /// Gets the body of the message.
    /// </summary>
    public MBoxBody Body { get; }

    /// <summary>
    /// Gets the problems found while reading the message.
    /// </summary>
    public IReadOnlyList<MBoxDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxMessage"/> class.
    /// </summary>
    /// <param name="record">
    /// The location of the message inside the mailbox.
    /// </param>
    /// <param name="headers">
    /// The header fields of the message.
    /// </param>
    /// <param name="body">
    /// The body of the message.
    /// </param>
    /// <param name="diagnostics">
    /// The problems found while reading the message.
    /// </param>
    public MBoxMessage(MBoxRecord record,
                       MBoxHeaders headers,
                       MBoxBody body,
                       IReadOnlyList<MBoxDiagnostic> diagnostics) {
        Record = record ?? throw new ArgumentNullException(nameof(record));
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        Body = body ?? throw new ArgumentNullException(nameof(body));
        Diagnostics = MBoxCollection.Own(diagnostics);
    }
}
