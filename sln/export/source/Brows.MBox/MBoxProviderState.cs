namespace Brows;

/// <summary>
/// Holds the state of a mailbox provider.
/// </summary>
internal enum MBoxProviderState {
    /// <summary>
    /// The provider is reading the mailbox.
    /// </summary>
    Loading = 0,

    /// <summary>
    /// The provider read the mailbox, and every message was indexed.
    /// </summary>
    Ready = 1,

    /// <summary>
    /// The mailbox holds no message.
    /// </summary>
    Empty = 2,

    /// <summary>
    /// The provider indexed some of the messages, and a limit or a problem stopped it.
    /// </summary>
    Partial = 3,

    /// <summary>
    /// The reading of the mailbox was cancelled.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// The mailbox could not be read.
    /// </summary>
    Failed = 5,
}
