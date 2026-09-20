using Brows.CLI;
using Brows.Config;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.IPC;

/// <summary>
/// A wrapper for the process that runs host commands.
/// </summary>
public sealed class HostProcess {
    private static readonly ILog Log = Logging.For(typeof(HostProcess));
    private static readonly IEnumerable<char> InvalidPathChars = Path.GetInvalidPathChars();

    private string PathSafeId => field ??= PathSafe(Id);
    private string PathSafeName => field ??= PathSafe(Name);
    private string HostRoot => field ??= Common.GetHostRoot(Name);

    private HostProcessParam Common => field ??=
        Param?.Common ?? new();

    private static string PathSafe(string s) {
        foreach (var c in InvalidPathChars) {
            s = s?.Replace(c, '_');
        }
        return s;
    }

    private Task ClearOldHosts(CancellationToken token) {
        /*
         * Delete directories past a certain age, so the disk doesn't fill up.
         */
        var now = DateTime.UtcNow;
        var dirs = Directory.GetDirectories(HostRoot);
        var skip = HomeDirectory;
        var tasks = dirs.Select(dir => Task.Run(cancellationToken: token, action: () => {
            if (true == dir?.Equals(skip, StringComparison.OrdinalIgnoreCase)) {
                /*
                 * Don't delete this directory!
                 */
                return;
            }
            var info = new DirectoryInfo(dir);
            if (info.Exists) {
                var then = info.LastWriteTimeUtc;
                var diff = now - then;
                if (diff > TimeSpan.FromDays(7)) {
                    info.Delete(recursive: true);
                }
            }
        }));
        return Task.WhenAll(tasks);
    }

    private async Task Run(CancellationToken token) {
        var processDir = HomeDirectory;
        var userConfigDir = Path.Combine(Common.GetNameRoot(Name, ConfigKind.User), "Config");
        var commonConfigDir = Path.Combine(Common.GetNameRoot(Name, ConfigKind.Common), "Config");
        await Task.Run(cancellationToken: token, action: () => {
            Directory.CreateDirectory(processDir);
            Directory.CreateDirectory(userConfigDir);
            Directory.CreateDirectory(commonConfigDir);
        });
        /*
         * Start host-directory cleanup concurrently. It runs in the
         * background while the main program executes and is only
         * awaited in the finally block during teardown.
         */
        var clearOldHosts = ClearOldHosts(token);
        /*
         * This process uses the CLI program framework to initialize
         * and run the process. Using it does NOT mean the caller has
         * to be a CLI program. Any program (e.g. a windowed program)
         * can run the process, but using the CLI framework simplifies
         * some things and allows the environment to be set up correctly
         * with minimal effort.
         */
        var program = new CliProgram(new() {
            LogFile = new() {
                Directory = Path.Combine(HomeDirectory, "Log"),
                Name = PathSafeName + ".log",
                Threshold = Common?.LogFileThreshold
            },
            ImportVariables = new(
                /*
                 * This implementation of `IExportVariable` sets the paths used by the
                 * config framework for configuration files.
                 */
                new ConfigExportVariable {
                    UserConfig = new() { Path = userConfigDir },
                    CommonConfig = new() { Path = commonConfigDir },
                    DefaultConfig = Common?.Config,
                }),
            Param = Param,
            ProgramName = Name,
            Token = token,
        });
        try {
            await program.Task;
        }
        finally {
            try {
                /*
                 * Allow deletion of old host data to complete.
                 */
                await clearOldHosts;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                /*
                 * Swallow cancellation silently.
                 */
            }
            catch (Exception ex) {
                /*
                 * Don't throw here. Just log the error and exit. Old host data may
                 * have to be deleted manually.
                 */
                Log.Warn(ex);
            }
        }
    }

    /// <summary>
    /// Gets the home directory of the host.
    /// </summary>
    public string HomeDirectory => field ??= Path.Combine(HostRoot, PathSafeId);

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
    /// Gets the name of the process.
    /// </summary>
    /// <remarks>
    /// This is not necessarily the same as the OS process name.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Gets the ID of the process.
    /// </summary>
    /// <remarks>
    /// This is not the same as the OS process ID.
    /// </remarks>
    public string Id { get; }

    /// <summary>
    /// Gets the GUID of the process.
    /// </summary>
    public Guid Guid { get; }

    /// <summary>
    /// Gets the parameter passed to the process, if one exists.
    /// </summary>
    public HostCommandParam Param { get; }

    /// <summary>
    /// Gets the cancellation token for the process.
    /// </summary>
    public CancellationToken Token { get; }

    /// <summary>
    /// Gets the process's task.
    /// </summary>
    /// <remarks>
    /// This task will complete when the process finishes.
    /// </remarks>
    public Task Task { get; }

    /// <summary>
    /// Creates a new host process.
    /// </summary>
    /// <param name="name">The name of the process.</param>
    /// <param name="param">An optional command parameter.</param>
    /// <param name="token">The cancellation token for the process task.</param>
    public HostProcess(string name = null, HostCommandParam param = null, CancellationToken token = default) {
        Token = token;
        Param = param;
        Name = name;
        Guid = Guid.NewGuid();
        Id = string.IsNullOrWhiteSpace(Name)
            ? $"{Guid:N}"
            : $"{Name}_{Guid:N}"; // More than one process with a given name could be running at the same time,
                                  // so we need to differentiate them. We do this with a GUID.
        /*
         * The task starts immediately. Callers should await the Task
         * property to observe completion, cancellation, or failure.
         */
        Task = Run(Token);
    }
}
