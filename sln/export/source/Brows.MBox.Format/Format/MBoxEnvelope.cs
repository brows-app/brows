using System;
using System.Collections.Generic;

namespace Brows.MBox.Format;

/// <summary>
/// Describes the envelope line that introduces a stored message.
/// </summary>
public sealed record MBoxEnvelope {
    /// <summary>
    /// Gets the opaque sender token of the envelope. The mbox <c>-</c> sender is reported as an
    /// empty string; no address is invented for it.
    /// </summary>
    public string Sender { get; }

    /// <summary>
    /// Gets the raw envelope line, without its line terminator.
    /// </summary>
    public string Raw { get; }

    /// <summary>
    /// Gets the date of the envelope, or <see langword="null"/> when the date is missing or invalid.
    /// </summary>
    public DateTimeOffset? Date { get; }

    /// <summary>
    /// Gets the problems found while interpreting the envelope.
    /// </summary>
    public IReadOnlyList<MBoxDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxEnvelope"/> class.
    /// </summary>
    /// <param name="sender">
    /// The opaque sender token of the envelope.
    /// </param>
    /// <param name="raw">
    /// The raw envelope line, without its line terminator.
    /// </param>
    /// <param name="date">
    /// The date of the envelope, or <see langword="null"/> when the date is missing or invalid.
    /// </param>
    /// <param name="diagnostics">
    /// The problems found while interpreting the envelope.
    /// </param>
    public MBoxEnvelope(string sender,
                         string raw,
                         DateTimeOffset? date,
                         IReadOnlyList<MBoxDiagnostic> diagnostics) {
        Sender = sender ?? string.Empty;
        Raw = raw ?? string.Empty;
        Date = date;
        Diagnostics = MBoxCollection.Own(diagnostics);
    }
}
