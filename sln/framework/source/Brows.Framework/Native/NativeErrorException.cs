namespace Brows.Native;

/// <summary>
/// Reports an error code returned by a native operation.
/// </summary>
public sealed class NativeErrorException : Exception {
    /// <summary>
    /// Gets the native error code.
    /// </summary>
    public int Error { get; }

    /// <summary>
    /// Initializes an exception for a native error code.
    /// </summary>
    /// <param name="error">
    /// The native error code.
    /// </param>
    public NativeErrorException(int error) {
        Error = error;
    }
}
