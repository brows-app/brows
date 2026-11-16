using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class ProtocolTestServerInfrastructureTest {
    [Test]
    public void FindPython_UsesTheExplicitOverride() {
        var result = ProtocolTestServerInfrastructure.FindPython(
            "repository",
            "local-app-data",
            "python-from-path",
            _ => false);

        Assert.That(result, Is.EqualTo("python-from-path"));
    }

    [Test]
    public void FindPython_FindsTheDependencyInstallersDefaultEnvironment() {
        var localAppData = Path.Combine(Path.GetTempPath(), "brows-protocol-tests", "local-app-data");
        var expected = Path.Combine(
            localAppData,
            "Brows",
            "ProtocolTestServers",
            "venv",
            "Scripts",
            "python.exe");

        var result = ProtocolTestServerInfrastructure.FindPython(
            Path.Combine(Path.GetTempPath(), "brows-protocol-tests", "checkout"),
            localAppData,
            null,
            path => string.Equals(path, expected, StringComparison.OrdinalIgnoreCase));

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void FindPython_FindsTheLegacyEnvironmentAboveANestedWorktree() {
        var repositoryRoot = Path.Combine(
            Path.GetTempPath(),
            "brows-protocol-tests",
            "checkout",
            "sln",
            "out",
            "worktrees",
            "review-25");
        var sharedEnvironment = Path.Combine(
            Path.GetTempPath(),
            "brows-protocol-tests",
            "checkout",
            "sln",
            "out",
            "test-server-venv",
            "Scripts",
            "python.exe");
        var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            sharedEnvironment
        };

        var result = ProtocolTestServerInfrastructure.FindPython(
            repositoryRoot,
            Path.Combine(Path.GetTempPath(), "brows-protocol-tests", "empty-local-app-data"),
            null,
            existingPaths.Contains);

        Assert.That(result, Is.EqualTo(sharedEnvironment));
    }

    [Test]
    public void FindPython_FallsBackToPythonOnPath() {
        var result = ProtocolTestServerInfrastructure.FindPython(
            Path.Combine(Path.GetTempPath(), "brows-protocol-tests", "checkout"),
            Path.Combine(Path.GetTempPath(), "brows-protocol-tests", "empty-local-app-data"),
            null,
            _ => false);

        Assert.That(result, Is.EqualTo("python"));
    }

    [Test]
    public void FindServerScript_FindsTheRepositoryScriptFromNestedTestOutput() {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "brows-protocol-tests", "checkout");
        var applicationBaseDirectory = Path.Combine(repositoryRoot, "sln", "export", "tests", "bin", "Release");
        var expected = Path.Combine(repositoryRoot, "scripts", "protocol-test-servers", "server.py");

        var result = ProtocolTestServerInfrastructure.FindServerScript(
            applicationBaseDirectory,
            null,
            null,
            path => string.Equals(path, expected, StringComparison.OrdinalIgnoreCase));

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public async Task WaitForReadinessAsync_ReadsTheCompletedReadinessFile() {
        var rootPath = Path.Combine(Path.GetTempPath(), "brows-protocol-readiness-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        var readinessPath = Path.Combine(rootPath, "ready.json");
        const string readinessJson = "{\"host\":\"127.0.0.1\"}";

        try {
            using var process = Process.GetCurrentProcess();
            var writeReadiness = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var writerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var writingTask = Task.Run(async () => {
                writerStarted.TrySetResult();
                await writeReadiness.Task.WaitAsync(TimeSpan.FromSeconds(2));
                var temporaryPath = readinessPath + ".tmp";
                await File.WriteAllTextAsync(temporaryPath, readinessJson);
                File.Move(temporaryPath, readinessPath);
            });
            await writerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            var readinessTask = ProtocolTestServerInfrastructure.WaitForReadinessAsync(
                process,
                readinessPath,
                TimeSpan.FromSeconds(2),
                default);
            writeReadiness.TrySetResult();
            var result = await readinessTask.WaitAsync(TimeSpan.FromSeconds(2));
            await writingTask.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.That(result, Is.EqualTo(readinessJson));
        }
        finally {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}
