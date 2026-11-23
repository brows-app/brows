using System;

namespace Brows.Entries;

/// <summary>
/// Provides a disposable lifetime for resources prepared for stream access.
/// </summary>
public class EntryStreamReady : IEntryStreamReady {
    /// <summary>
    /// Releases resources associated with stream preparation.
    /// </summary>
    /// <param name="disposing">
    /// True for explicit disposal; false when called during finalization.
    /// </param>
    protected virtual void Dispose(bool disposing) {
    }

    /// <summary>
    /// Releases the resources associated with stream preparation.
    /// </summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources associated with stream preparation during finalization.
    /// </summary>
    ~EntryStreamReady() {
        Dispose(false);
    }
}
