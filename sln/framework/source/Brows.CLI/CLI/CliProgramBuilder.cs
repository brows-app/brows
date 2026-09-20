using System;
using System.IO;
using System.Threading;

namespace Brows.CLI;

/// <summary>
/// A helper for building a CLI program.
/// </summary>
public sealed class CliProgramBuilder {
    private static string EnvironmentProgramName(out string programPath) {
        programPath = Environment.GetCommandLineArgs()[0];
        return Path.GetFileNameWithoutExtension(programPath);
    }

    private static string EnvironmentCommandLine() {
        // Get the "raw" command-line input by stripping the .exe path
        // from the environment's command-line value. The .exe path may
        // be quoted.
        var commandLine = Environment.CommandLine;
        var programName = EnvironmentProgramName(out var programPath);
        var programPathQuoted = $"\"{programPath}\"";
        var cl = commandLine.StartsWith(programPathQuoted)
            ? commandLine.Substring(programPathQuoted.Length)
            : commandLine.Substring(programPath.Length);
        return cl;
    }

    /// <summary>
    /// Gets or sets the command-line input to the program.
    /// </summary>
    /// <remarks>
    /// If this property is left unset, the command line of
    /// the currently executing process is used.
    /// </remarks>
    public string CommandLine {
        get => field ??= EnvironmentCommandLine();
        set => field = value;
    }

    /// <summary>
    /// Gets or sets the program-name input to the program.
    /// </summary>
    /// <remarks>
    /// If this property is left unset, the program name of
    /// the currently executing process is used.
    /// </remarks>
    public string ProgramName {
        get => field ??= EnvironmentProgramName(out _);
        set => field = value;
    }

    /// <summary>
    /// Gets or sets the command parameter.
    /// </summary>
    public ICliCommandParam Param { get; set; }

    /// <summary>
    /// Gets or sets variables for the composition framework.
    /// </summary>
    public ImportVariables ImportVariables { get; set; }

    /// <summary>
    /// Gets or sets information about the program's log file.
    /// </summary>
    public CliLogFile LogFile { get; set; }

    /// <summary>
    /// Gets or sets information about the program's console output.
    /// </summary>
    public CliLogConsole LogConsole { get; set; }

    /// <summary>
    /// Gets or sets the cancellation token for the program.
    /// </summary>
    public CancellationToken Token { get; set; }
}
