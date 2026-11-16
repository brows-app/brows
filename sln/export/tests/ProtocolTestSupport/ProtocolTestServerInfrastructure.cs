using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal static class ProtocolTestServerInfrastructure {
    private static readonly TimeSpan ReadinessPollInterval = TimeSpan.FromMilliseconds(50);

    internal static string FindServerScript(
        string applicationBaseDirectory,
        string currentDirectory,
        string overridePath,
        Func<string, bool> fileExists) {
        ArgumentNullException.ThrowIfNull(fileExists);

        if (!string.IsNullOrWhiteSpace(overridePath) && fileExists(overridePath)) {
            return Path.GetFullPath(overridePath);
        }

        foreach (var startPath in new[] { applicationBaseDirectory, currentDirectory }) {
            if (string.IsNullOrWhiteSpace(startPath)) {
                continue;
            }

            for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent) {
                var path = Path.Combine(directory.FullName, "scripts", "protocol-test-servers", "server.py");
                if (fileExists(path)) {
                    return path;
                }
            }
        }

        return null;
    }

    internal static string FindRepositoryRoot(string serverScriptPath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverScriptPath);

        var scriptDirectory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(serverScriptPath)));
        for (var directory = scriptDirectory; directory is not null; directory = directory.Parent) {
            var solutionPath = Path.Combine(directory.FullName, "sln", "brows.slnx");
            if (File.Exists(solutionPath)) {
                return directory.FullName;
            }
        }

        return scriptDirectory?.Parent?.Parent?.FullName ??
               throw new DirectoryNotFoundException("Could not find the repository root for the protocol test server.");
    }

    internal static string FindPython(
        string repositoryRoot,
        string localApplicationDataPath,
        string overridePath,
        Func<string, bool> fileExists) {
        ArgumentNullException.ThrowIfNull(fileExists);

        if (!string.IsNullOrWhiteSpace(overridePath)) {
            return overridePath;
        }

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(localApplicationDataPath)) {
            candidates.Add(Path.Combine(
                localApplicationDataPath,
                "Brows",
                "ProtocolTestServers",
                "venv",
                "Scripts",
                "python.exe"));
        }

        if (!string.IsNullOrWhiteSpace(repositoryRoot)) {
            for (var directory = new DirectoryInfo(repositoryRoot);
                 directory is not null;
                 directory = directory.Parent) {
                candidates.Add(Path.Combine(
                    directory.FullName,
                    "sln",
                    "out",
                    "test-server-venv",
                    "Scripts",
                    "python.exe"));
            }
        }

        var checkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates) {
            var fullPath = Path.GetFullPath(candidate);
            if (checkedPaths.Add(fullPath) && fileExists(fullPath)) {
                return fullPath;
            }
        }

        return "python";
    }

    internal static async Task<string> WaitForReadinessAsync(
        Process process,
        string readinessPath,
        TimeSpan timeout,
        CancellationToken token) {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentException.ThrowIfNullOrWhiteSpace(readinessPath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var stopwatch = Stopwatch.StartNew();
        while (true) {
            token.ThrowIfCancellationRequested();
            if (File.Exists(readinessPath)) {
                return await File.ReadAllTextAsync(readinessPath, token).ConfigureAwait(false);
            }

            if (process.HasExited) {
                throw new InvalidOperationException(
                    $"The protocol test server exited before readiness with code {process.ExitCode}.");
            }

            var remaining = timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero) {
                throw new TimeoutException(
                    $"The protocol test server did not become ready within {timeout.TotalSeconds:0} seconds.");
            }

            var delay = remaining < ReadinessPollInterval ? remaining : ReadinessPollInterval;
            await Task.Delay(delay, token).ConfigureAwait(false);
        }
    }
}
