using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Brows.Updates;

internal sealed class UpdaterHandoff {
    private const string Installed = "Installed";
    private const string RestartRequired = "RestartRequired";
    private const string Failed = "Failed";
    private const string NotStarted = "NotStarted";

    private UpdaterArguments Args { get; }

    private static bool WaitForBrows(int pid, TimeSpan timeout) {
        Process process;
        try {
            process = Process.GetProcessById(pid);
        }
        catch (ArgumentException) {
            return true; // Brows exited before the helper opened the process.
        }
        using (process) {
            return process.WaitForExit((int)timeout.TotalMilliseconds);
        }
    }

    private static int Install(ProcessStartInfo info) {
        using (var process = Process.Start(info)
            ?? throw new InvalidOperationException("Windows Installer could not be started.")) {
            process.WaitForExit();
            return process.ExitCode;
        }
    }

    private static void Launch(ProcessStartInfo info) {
        Process.Start(info)?.Dispose();
    }

    private void Write(string kind, int exitCode) {
        var temp = Args.Outcome + ".tmp";
        try {
            File.WriteAllText(temp, JsonSerializer.Serialize(new {
                version = Args.Version.Text,
                kind,
                exitCode,
                log = Args.Log
            }));
            File.Move(temp, Args.Outcome, overwrite: true);
        }
        finally {
            File.Delete(temp);
        }
    }

    private void CleanOldVersions() {
        foreach (var path in Directory.EnumerateDirectories(Args.Updates)) {
            var name = Path.GetFileName(path);
            var older = AppVersion.TryParse(name, out var version) && version.Text == name
                && Args.Version.IsNewerThan(version);
            if (older) {
                BestEffort(() => Directory.Delete(path, recursive: true));
            }
        }
    }

    private static void BestEffort(Action action) {
        try {
            action();
        }
        catch (Exception ex) {
            Trace.TraceError(ex.ToString());
        }
    }

    internal const string MutexName = @"Local\Brows.Update";

    internal TimeSpan ExitTimeout { get; init; } = TimeSpan.FromSeconds(120);
    internal Func<int, TimeSpan, bool> Wait { get; init; } = WaitForBrows;
    internal Func<ProcessStartInfo, int> RunInstaller { get; init; } = Install;
    internal Action<ProcessStartInfo> LaunchBrows { get; init; } = Launch;

    internal UpdaterHandoff(UpdaterArguments args) {
        Args = args ?? throw new ArgumentNullException(nameof(args));
    }

    internal int Run() {
        using (var mutex = new Mutex(initiallyOwned: true, MutexName, out var created)) {
            if (!created) {
                return 3;
            }

            var attempted = false;
            try {
                if (!Wait(Args.Pid, ExitTimeout)) {
                    Write(NotStarted, 0);
                    return 1;
                }
                var info = UpdaterMSIExec.StartInfo(Args.Msi, Args.Log);
                attempted = true;
                var code = RunInstaller(info);
                var kind = code switch {
                    0 => Installed,
                    3010 => RestartRequired,
                    _ => Failed
                };
                Write(kind, code);
                if (kind is Installed or RestartRequired) {
                    BestEffort(CleanOldVersions);
                }
                return code;
            }
            catch (Exception ex) {
                Trace.TraceError(ex.ToString());
                BestEffort(() => Write(Failed, -1));
                return 1;
            }
            finally {
                if (attempted && File.Exists(Args.Launch)) {
                    BestEffort(() => LaunchBrows(new ProcessStartInfo(Args.Launch) {
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(Args.Launch)
                    }));
                }
                mutex.ReleaseMutex();
            }
        }
    }
}
