using System;
using System.Collections.Generic;

namespace Brows.MBox.Format;

/// <summary>
/// Describes where a stored message is located inside a mailbox.
/// </summary>
internal sealed record MBoxRecord {
    /// <summary>
    /// Gets the absolute offset, from the start of the mailbox, of the envelope line.
    /// </summary>
    internal long Offset { get; }

    /// <summary>
    /// Gets the length, in bytes, of the whole stored record, including its envelope and framing.
    /// </summary>
    internal long Length { get; init; }

    /// <summary>
    /// Gets the absolute offset, from the start of the mailbox, of the first message byte.
    /// </summary>
    internal long MessageOffset { get; }

    /// <summary>
    /// Gets the length, in bytes, of the message, excluding its envelope and framing.
    /// </summary>
    internal long MessageLength { get; init; }

    /// <summary>
    /// Gets the absolute offset, from the start of the mailbox, of the first body byte, or
    /// <see langword="null"/> when the end of the header block cannot be found.
    /// </summary>
    internal long? BodyOffset { get; init; }

    /// <summary>
    /// Gets the mailbox storage dialect the record was read with.
    /// </summary>
    internal MBoxFormat Format { get; }

    /// <summary>
    /// Gets the envelope that introduces the record.
    /// </summary>
    internal MBoxEnvelope Envelope { get; }

    /// <summary>
    /// Gets the problems found while reading the record.
    /// </summary>
    internal IReadOnlyList<MBoxDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxRecord"/> class.
    /// </summary>
    /// <param name="offset">
    /// The absolute offset, from the start of the mailbox, of the envelope line.
    /// </param>
    /// <param name="length">
    /// The length, in bytes, of the whole stored record.
    /// </param>
    /// <param name="messageOffset">
    /// The absolute offset, from the start of the mailbox, of the first message byte.
    /// </param>
    /// <param name="messageLength">
    /// The length, in bytes, of the message.
    /// </param>
    /// <param name="bodyOffset">
    /// The absolute offset, from the start of the mailbox, of the first body byte.
    /// </param>
    /// <param name="format">
    /// The mailbox storage dialect the record was read with.
    /// </param>
    /// <param name="envelope">
    /// The envelope that introduces the record.
    /// </param>
    /// <param name="diagnostics">
    /// The problems found while reading the record.
    /// </param>
    internal MBoxRecord(long offset,
                      long length,
                      long messageOffset,
                      long messageLength,
                      long? bodyOffset,
                      MBoxFormat format,
                      MBoxEnvelope envelope,
                      IReadOnlyList<MBoxDiagnostic> diagnostics) {
        Offset = offset;
        Length = length;
        MessageOffset = messageOffset;
        MessageLength = messageLength;
        BodyOffset = bodyOffset;
        Format = format;
        Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
        Diagnostics = MBoxCollection.Own(diagnostics);
    }

    /// <summary>
    /// Gets the absolute offset, from the start of the mailbox, at which the record ends.
    /// </summary>
    internal long End =>
        checked(Offset + Length);
}
