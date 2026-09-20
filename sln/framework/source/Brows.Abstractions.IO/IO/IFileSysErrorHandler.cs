namespace Brows.IO;

/// <summary>
/// A contract for handling file-system errors.
/// </summary>
public interface IFileSysErrorHandler {
    /// <summary>
    /// Handles a file-system error.
    /// </summary>
    /// <param name="error">The error handle.</param>
    /// <param name="isCancellation">
    /// A flag that indicates whether or not the error is a
    /// result of a cancelled operation.
    /// </param>
    void Handle(FileSysError error, bool isCancellation);
}
