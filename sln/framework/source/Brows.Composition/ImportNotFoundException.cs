namespace Brows;

/// <summary>
/// The exception that is thrown when an instance of a requested type
/// is not found by the extension framework.
/// </summary>
public sealed class ImportNotFoundException : Exception {
    internal ImportNotFoundException(Type typeNotFound) {
        TypeNotFound = typeNotFound;
    }

    /// <summary>
    /// Gets the exception message.
    /// </summary>
    public sealed override string Message =>
        $"An import of type '{TypeNotFound}' was requested but not found.";

    /// <summary>
    /// Gets the type of which an instance was not found.
    /// </summary>
    public Type TypeNotFound { get; }
}
