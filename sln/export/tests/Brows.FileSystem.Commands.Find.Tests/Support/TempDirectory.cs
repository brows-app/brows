using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Support;

internal sealed class TempDirectory : IDisposable {
    private readonly List<(string Link, string Target)> Junctions = new();

    private string UnderRoot(string path) {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetFullPath(Root.FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootPrefix = root + Path.DirectorySeparatorChar;
        var pathIsRoot = string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase);
        var pathIsNested = fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
        if (!pathIsRoot && !pathIsNested) {
            throw new ArgumentException($"Path [{fullPath}] is outside the temporary directory.", nameof(path));
        }
        return fullPath;
    }

    internal DirectoryInfo Root { get; }

    internal TempDirectory() {
        var path = Path.Combine(Path.GetTempPath(), $"Brows.Find.Tests.{Guid.NewGuid():N}");
        Root = Directory.CreateDirectory(path);
    }

    internal DirectoryInfo CreateDirectory(string relativePath) {
        var path = UnderRoot(Path.Combine(Root.FullName, relativePath));
        return Directory.CreateDirectory(path);
    }

    internal FileInfo CreateFile(string relativePath, string contents = "") {
        var path = UnderRoot(Path.Combine(Root.FullName, relativePath));
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) {
            Directory.CreateDirectory(parent);
        }
        File.WriteAllText(path, contents);
        return new FileInfo(path);
    }

    internal DirectoryInfo CreateJunction(string relativePath, DirectoryInfo target) {
        ArgumentNullException.ThrowIfNull(target);
        if (!OperatingSystem.IsWindows()) {
            throw new PlatformNotSupportedException("Directory junction tests require Windows.");
        }

        var path = UnderRoot(Path.Combine(Root.FullName, relativePath));
        var targetPath = UnderRoot(target.FullName);
        var pathAlreadyExists = Directory.Exists(path) || File.Exists(path);
        if (pathAlreadyExists) {
            throw new IOException($"Cannot create a junction at existing path [{path}].");
        }
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) {
            Directory.CreateDirectory(parent);
        }
        Junctions.Add((path, targetPath));

        var startInfo = new ProcessStartInfo {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(path);
        startInfo.ArgumentList.Add(targetPath);
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardOutput = true;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start cmd.exe to create a junction.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit(milliseconds: 10000);
        if (!exited) {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("Timed out creating a temporary directory junction.");
        }
        Task.WaitAll(output, error);
        if (process.ExitCode != 0) {
            throw new InvalidOperationException(
                $"Could not create junction at [{path}]: {output.Result.Trim()} {error.Result.Trim()}");
        }

        return new DirectoryInfo(path);
    }

    public void Dispose() {
        for (var i = Junctions.Count - 1; i >= 0; i--) {
            try {
                var junction = Junctions[i];
                UnderRoot(junction.Link);
                UnderRoot(junction.Target);
                Directory.Delete(junction.Link, recursive: false);
            }
            catch (DirectoryNotFoundException) {
            }
        }

        var root = UnderRoot(Root.FullName);
        if (Directory.Exists(root)) {
            Directory.Delete(root, recursive: true);
        }
    }
}
