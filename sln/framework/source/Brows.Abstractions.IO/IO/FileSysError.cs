using System;

namespace Brows.IO;

/// <summary>
/// A file-system error.
/// </summary>
/// <param name="path">The file-system path associated with the error.</param>
/// <param name="exception">The error exception.</param>
public sealed class FileSysError(string path, Exception exception) {
    /// <summary>
    /// Gets the file-system path associated with the error.
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Gets the error exception.
    /// </summary>
    public Exception Exception { get; } = exception;

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message => Exception?.Message;
}
