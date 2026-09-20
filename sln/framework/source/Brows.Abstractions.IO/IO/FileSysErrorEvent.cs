using System;

namespace Brows.IO;

/// <summary>
/// Delegate for handling file-system error events.
/// </summary>
/// <param name="sender">The object that raised the event.</param>
/// <param name="e">The event arguments.</param>
public delegate void FileSysErrorEventHandler(object sender, FileSysErrorEventArgs e);

/// <summary>
/// Event arguments for file-system error events.
/// </summary>
/// <param name="error">The file-system error.</param>
/// <param name="isCancellation">
/// A flag that indicates whether or not the error is a
/// result of a cancelled operation.
/// </param>
public sealed class FileSysErrorEventArgs(FileSysError error, bool isCancellation) : EventArgs {
    /// <summary>
    /// Gets the file-system error.
    /// </summary>
    public FileSysError Error { get; } = error;

    /// <summary>
    /// Gets a flag that indicates whether or not the error is a
    /// result of a cancelled operation.
    /// </summary>
    public bool IsCancellation { get; } = isCancellation;
}
