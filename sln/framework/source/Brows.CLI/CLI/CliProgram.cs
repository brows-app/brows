using Domore.Conf;
using Domore.Conf.Cli;
using Domore.Conf.Logs;
using Domore.Logs;
using Domore.Threading.Tasks;
using Brows.Composition;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.CLI;

public sealed class CliProgram : IImportEnvironment {
    private static readonly ILog Log = Logging.For(typeof(CliProgram));

    private readonly TaskCache<CliProgramConf> Conf = new(token => Task.Run(cancellationToken: token, function: () => {
        var conf = (IConf)new Conf();
        return conf.Configure(new CliProgramConf(), key: "");
    }));

    private CliSetup CliSetup => field ??=
        new CliSetup()
            .WithCommandSpace(_ => ProgramName)
            .WithCommandName(type => {
                if (type is null) {
                    return null;
                }
                var declaringType = type.DeclaringType;
                if (declaringType is null) {
                    return null;
                }
                var extendsUtilCommand = typeof(CliCommand).IsAssignableFrom(declaringType);
                if (extendsUtilCommand == false) {
                    return null;
                }
                if (declaringType.Name.EndsWith("Command") == false) {
                    return declaringType.Name.ToLowerInvariant();
                }
                return declaringType
                    .Name
                    .Substring(0, declaringType.Name.Length - "Command".Length)
                    .ToLowerInvariant();
            });

    private CliProvider Cli => field ??= new(CliSetup);

    private CliCommandContext CommandContext => field ??= Param switch {
        /*
         * When a command-parameter object is given, we can use that
         * to construct the context.
         */
        var param when param is not null => new(Cli, ProgramName, param),
        /*
         * No command-parameter object was specified, so we're going to 
         * look at the command line and parse out a parameter from that.
         * This will be the case if the program is a bona-fide CLI app
         * run from the console, for example.
         */
        _ => new Func<CliCommandContext>(() => {
            /*
             * The name of the command is the first span of characters
             * that precedes whitespace. The remaining characters are
             * considered input to that command.
             */
            var cmd = CommandLine?.Trim() ?? "";
            var cmdPart = cmd.Split([], count: 2).Select(part => part.Trim()).Where(part => part != "").ToList();
            var cmdName = cmdPart.Count > 0 ? cmdPart[0] : "about";
            var cmdArgs = cmdPart.Count > 1 ? cmdPart[1] : "";
            var cmdLine = $"{cmdName} {cmdArgs}";
            return new(Cli, ProgramName, cmdLine);
        })()
    };

    private void Logging_Event(object sender, LogEventArgs e) {
        if (e is null) {
            return;
        }
        var severity = e.LogSeverity;
        if (severity == LogSeverity.None) {
            return;
        }
        if (severity < LogConsole.Threshold) {
            return;
        }
        switch (e.LogName) {
            case "ConfigPath":
            case "ConfigFileInfo":
            case "Imports":
            case "ImportsState":
            case "ImportsAgent":
            case "ImportCollection":
            case "ComposedImportCollectionFactory":
                /*
                 * All of the logged types above will just pollute the console's output, 
                 * so they're mostly ignored.
                 */
                if (severity < LogSeverity.Error) {
                    break;
                }
                goto default;
            default:
                Console.BackgroundColor = severity switch {
                    LogSeverity.Critical => ConsoleColor.Gray,
                    _ => ConsoleColor.Black
                };
                Console.ForegroundColor = severity switch {
                    LogSeverity.Critical => ConsoleColor.Black,
                    LogSeverity.Debug => ConsoleColor.Blue,
                    LogSeverity.Error => ConsoleColor.Red,
                    LogSeverity.Info => ConsoleColor.Gray,
                    LogSeverity.Warn => ConsoleColor.Yellow,
                    _ => ConsoleColor.White
                };
                var list = e?.LogList;
                if (list is not null) {
                    foreach (var item in list) {
                        var isCliCommandOutput = CliCommandOutput.TryGetData(item, out var data);
                        if (isCliCommandOutput) {
                            Console.Out.WriteLine(data);
                        }
                        else {
                            Console.Error.WriteLine(item);
                        }
                    }
                }
                break;
        }
    }

    private async Task OnLogging(Func<CancellationToken, Task> function, CancellationToken token) {
        /*
         * This method initializes logging for the program. It sets log paths, thresholds, etc.
         * The callback function does the actual work of the program, so this may be a long-
         * running task.
         */
        var conf = await Conf.Ready(token);
        var logFileThreshold = LogFile?.Threshold ?? conf.LogFileThreshold ?? LogSeverity.Warn;
        var logFileDirectory = LogFile?.Directory?.Trim() ?? "";
        if (logFileDirectory == "") {
            logFileDirectory = conf.LogFileDirectory?.Trim() ?? "";
        }
        var logFileName = LogFile?.Name?.Trim() ?? "";
        if (logFileName == "") {
            logFileName = conf.LogFileName?.Trim() ?? "";
        }
        Logging.Event += Logging_Event;
        Logging.EventThreshold = LogConsole.Threshold;
        LogConf.ConfigureLogging(LogFile is null ? "" : @"
            log[file].type                          = file
            log[file].config.default.format         = {loc.dat} {loc.tim} ({sev}) [{log}]
            log[file].config.default.severity       = " + logFileThreshold + @"
            log[file].service.directory             = " + logFileDirectory + @"
            log[file].service.name                  = " + logFileName + @"
            log[file].service.flushInterval         = 00:00:02.5
            log[file].service.fileSizeLimit         = 100000
            log[file].service.totalSizeLimit        = 10000000
        ");
        try {
            if (Log.Debug()) {
                Log.Debug($"Logging configured");
            }
            var task = function?.Invoke(token);
            if (task is not null) {
                await task;
            }
        }
        finally {
            try {
                Logging.Event -= Logging_Event;
                Logging.Complete();
            }
            catch (Exception ex) {
                /*
                 * Don't throw here. We shouldn't get here. If we do, it means something is messed
                 * up in the logging framework.
                 */
                Console.WriteLine(ex);
            }
            finally {
                /*
                 * This is necessary so the user's console doesn't retain any colors modified by
                 * the program after the program ends.
                 */
                Console.ResetColor();
            }
        }
    }

    private async Task OnCanceling(Func<CancellationToken, Task> function, CancellationToken token) {
        /*
         * This method sets up a cancellation opportunity for the program. If this is indeed a CLI
         * program with a console, cancellation may be triggered by the cancel-key press, e.g. Ctrl+C.
         * 
         * Cancellation wraps the callback function, which is the main function of the program, so
         * this may be a long-running task.
         */
        using (var cancelKeySource = new CancellationTokenSource()) {
            var
            console_CancelKeyPress = default(ConsoleCancelEventHandler);
            Console.CancelKeyPress += console_CancelKeyPress = (s, e) => {
                if (e is not null) {
                    /*
                     * Setting Cancel to true prevents the current process from actually terminating
                     * due to the key press. We want to prevent termination, because we want our own
                     * cancellation behavior, which is to cancel the running task.
                     */
                    e.Cancel = true;
                }
                cancelKeySource.Cancel();
            };
            try {
                using (var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(token, cancelKeySource.Token)) {
                    var task = function?.Invoke(linkedSource.Token);
                    if (task is not null) {
                        await task;
                    }
                }
            }
            finally {
                Console.CancelKeyPress -= console_CancelKeyPress;
            }
        }
    }

    private async Task OnImported(Func<CancellationToken, Task> function, CancellationToken token) {
        /*
         * This method initializes the composition (dependency injection, DI) framework, if it has
         * not already been initialized. The composition framework is required for the CLI program
         * to recognize commands.
         * 
         * The callback function is the main work of the program, so this may be a long-running task.
         */
        var imports = default(IImport);
        try {
            imports = await Imports.Init(this, token);
        }
        catch (ImportsAlreadyInitializedException) {
            /*
             * A caller may have already initialized imports, which is fine. Swallow this error
             * and continue on.
             */
        }
        Imports.Current.Populate(CommandContext);
        try {
            var task = function?.Invoke(token);
            if (task is not null) {
                await task;
            }
        }
        finally {
            if (imports is not null) {
                imports.Kill();
            }
        }
    }

    private async Task Run(CancellationToken token) {
        /*
         * This method runs the actual program and contains the last error handler that catches
         * any unhandled exceptions, so they can be logged and potentially shown to the user.
         * 
         * The idea is to initialize and de-initialize each of the logging, cancellation, and
         * composition frameworks that wrap the actual functionality of the program.
         */
        await OnLogging(token: token, function: async token => {
            await OnCanceling(token: token, function: async token => {
                await OnImported(token: token, function: async token => {
                    try {
                        Result = await CommandContext.Complete(token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) {
                        if (Log.Debug()) {
                            Log.Debug("Canceled");
                        }
                        Canceled = true;
                        ErrorCode = -2;
                    }
                    catch (Exception ex) {
                        if (Log.Debug()) {
                            Log.Debug(ex);
                        }
                        if (Log.Error()) {
                            Log.Error(new CliCommandOutput(ex?.Message));
                        }
                        Exception = ex;
                        ErrorCode = ex is CliCommandException ce
                            ? ce.ErrorCode
                            : -1;
                    }
                });
            });
        });
    }

    /// <summary>
    /// Gets the error code.
    /// </summary>
    public int ErrorCode { get; private set; }

    /// <summary>
    /// Gets the name of the program.
    /// </summary>
    public string ProgramName { get; }

    /// <summary>
    /// Gets the composition variables with which the instance was created.
    /// </summary>
    public ImportVariables ImportVariables { get; }

    /// <summary>
    /// Gets the process's task.
    /// </summary>
    /// <remarks>
    /// This task will complete when the process finishes.
    /// </remarks>
    public Task Task { get; }

    /// <summary>
    /// Gets the parameter passed to the process, if one exists.
    /// </summary>
    public ICliCommandParam Param { get; }

    /// <summary>
    /// Gets the command line passed to the program, if one exists.
    /// </summary>
    public string CommandLine { get; }

    /// <summary>
    /// Gets a flag that indicates whether or not the process has been canceled.
    /// </summary>
    public bool Canceled { get; private set; }

    /// <summary>
    /// Gets the result of the process.
    /// </summary>
    public object Result { get; private set; }

    /// <summary>
    /// Gets the exception that caused the process to end prematurely, if one exists.
    /// </summary>
    public Exception Exception { get; private set; }

    /// <summary>
    /// Gets the cancellation token with which the instance was created.
    /// </summary>
    public CancellationToken Token { get; }

    /// <summary>
    /// Gets the information about file logging with which the instance was created.
    /// </summary>
    public CliLogFile LogFile { get; }

    /// <summary>
    /// Gets the information about console logging with which the instance was created.
    /// </summary>
    public CliLogConsole LogConsole { get; }

    /// <summary>
    /// Gets the program builder that was used during construction of the instance.
    /// </summary>
    public CliProgramBuilder Builder { get; }

    /// <summary>
    /// Creates a new CLI program instance.
    /// </summary>
    /// <param name="builder">The builder with information for the CLI program instance.</param>
    public CliProgram(CliProgramBuilder builder = null) {
        Builder = builder ?? new();
        Param = Builder.Param;
        CommandLine = Param is not null
            ? Param.ToCommandLine()
            : Builder.CommandLine;
        ProgramName = Builder.ProgramName;
        ImportVariables = Builder.ImportVariables;
        LogConsole = Builder.LogConsole ?? new();
        LogFile = Builder.LogFile;
        Token = Builder.Token;
        Task = Run(Token);
    }

    /// <summary>
    /// Executes a CLI program with the specified parameters and returns a task representing the process execution.
    /// </summary>
    /// <param name="fileName">The name or path of the executable file to run.</param>
    /// <param name="commandLine">The command-line arguments to pass to the executable.</param>
    /// <param name="info">An object containing configuration and metadata for the CLI program.</param>
    /// <param name="token">A <see cref="CancellationToken"/> used to cancel the process execution.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation. The result contains the process
    /// execution data.
    /// </returns>
    public static Task<CliProgramData> Run(string fileName, string commandLine, CliProgramInfo info = null, CancellationToken token = default) {
        return new CliProgramWrapper(fileName, commandLine, info, token).Task;
    }

    /// <summary>
    /// Executes a CLI program with the specified parameters and returns a task representing the process execution.
    /// </summary>
    /// <param name="fileName">The name or path of the executable file to run.</param>
    /// <param name="param">The command parameter.</param>
    /// <param name="info">An object containing configuration and metadata for the CLI program.</param>
    /// <param name="token">A <see cref="CancellationToken"/> used to cancel the program execution.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation. The result contains the program
    /// execution data.
    /// </returns>
    public static Task<CliProgramData> Run(string fileName, ICliCommandParam param, CliProgramInfo info = null, CancellationToken token = default) {
        if (null == param) throw new ArgumentNullException(nameof(param));
        return Run(
            fileName: fileName,
            commandLine: param.ToCommandLine(),
            info,
            token);
    }

    ImportInfo IImportEnvironment.ImportInfo => ImportInfo.Composed(() => ImportVariables);
}
