using System;

namespace Brows.CLI;

/// <summary>
/// Represents configuration and behavior settings for a host process.
/// </summary>
/// <remarks>
/// This class provides options for configuring the behavior of a host process, including logging and
/// output handling. It is immutable after initialization, with properties set via object initializer syntax.
/// </remarks>
public sealed class CliProgramInfo {
    internal const int DefaultWaitTime = 10;
    internal const int DefaultMaxLogLength = 100000;
    internal const int DefaultMaxOutputLength = 100000;

    /// <summary>
    /// Gets or sets a flag that indiates whether or not an exception should be thrown upon
    /// cancellation of the program. If true, an exception will be thrown if the program is
    /// canceled. If false, no exception will be thrown.
    /// </summary>
    public bool ThrowIfCancellationRequested { get; init; }

    /// <summary>
    /// Gets or sets the amount of time, in milliseconds, to wait between checks for program
    /// exit.
    /// </summary>
    public int WaitTime { get; init; } = DefaultWaitTime;

    /// <summary>
    /// Gets or sets a flag that indicates whether or not the process is started in a window.
    /// </summary>
    public bool InWindow { get; init; }

    /// <summary>
    /// Gets or sets the maximum length of the process log data.
    /// </summary>
    public int MaxLogLength { get; init; } = DefaultMaxLogLength;

    /// <summary>
    /// Gets or sets the maximum length of the process output data.
    /// </summary>
    public int MaxOutputLength { get; init; } = DefaultMaxOutputLength;

    /// <summary>
    /// Gets or sets a callback invoked when data is logged.
    /// </summary>
    public Action<string> OnLog { get; init; }

    /// <summary>
    /// Gets or sets a callback invoked when data is outputted.
    /// </summary>
    public Action<string> OnOutput { get; init; }
}