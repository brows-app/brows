namespace Brows.CLI;

/// <summary>
/// The exception thrown when a CLI command parameter is invalid.
/// </summary>
public sealed class CliCommandParamException : CliCommandException {
    /// <summary>
    /// Gets the error code associated with the CLI error.
    /// </summary>
    public sealed override int ErrorCode => 2;

    /// <summary>
    /// Gets the CLI command parameter.
    /// </summary>
    public object Param { get; }

    /// <summary>
    /// Gets the name of the property for which the CLI command parameter is invalid.
    /// </summary>
    public string Property { get; }

    /// <summary>
    /// Creates a new instance of the exception.
    /// </summary>
    /// <param name="param">The CLI command parameter.</param>
    /// <param name="property">The name of the property for which the CLI command parameter is invalid.</param>
    /// <param name="message">The exception message.</param>
    public CliCommandParamException(object param, string property, string message) : base(message) {
        Param = param;
        Property = property;
    }
}
