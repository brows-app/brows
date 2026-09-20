using System;

namespace Brows.CLI;

/// <summary>
/// The base class for CLI command errors.
/// </summary>
public abstract class CliCommandException : Exception {
    protected CliCommandException() {
    }

    protected CliCommandException(string message) : base(message) {
    }

    /// <summary>
    /// Gets the error code associated with the CLI error.
    /// </summary>
    public abstract int ErrorCode { get; }
}
