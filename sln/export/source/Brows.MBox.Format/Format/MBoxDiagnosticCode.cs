namespace Brows.MBox.Format;

/// <summary>
/// Identifies the kind of problem found while reading a mailbox.
/// </summary>
public enum MBoxDiagnosticCode {
    /// <summary>
    /// The envelope line could not be interpreted.
    /// </summary>
    MalformedEnvelope,

    /// <summary>
    /// The envelope date was recognized syntactically but is not a valid date.
    /// </summary>
    MalformedDate,

    /// <summary>
    /// A header field was malformed.
    /// </summary>
    MalformedHeader,

    /// <summary>
    /// A header field exceeded the configured limits.
    /// </summary>
    HeaderLimitReached,

    /// <summary>
    /// A MIME part was malformed.
    /// </summary>
    MalformedMime,

    /// <summary>
    /// The declared character set could not be decoded strictly.
    /// </summary>
    MalformedEncoding,

    /// <summary>
    /// The declared character set is not supported.
    /// </summary>
    UnsupportedCharset,

    /// <summary>
    /// The body of the message is not available for display.
    /// </summary>
    UnsupportedBody,

    /// <summary>
    /// A configured limit was reached while reading.
    /// </summary>
    LimitReached,

    /// <summary>
    /// A record was recovered using bounded header lookahead.
    /// </summary>
    RecoveredRecord,

    /// <summary>
    /// A candidate separator could not be interpreted unambiguously and was kept as body data.
    /// </summary>
    AmbiguousRecovery,

    /// <summary>
    /// Some diagnostics were not retained because the diagnostic limit was reached.
    /// </summary>
    DiagnosticSummary,
}
