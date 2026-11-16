using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Brows;

internal sealed class GitTestSandbox : IDisposable {
    private readonly string Marker;
    private readonly string Root;
    private readonly string Hooks;
    private readonly string GlobalConfig;
    private readonly string Templates;
    private readonly Dictionary<string, string> OriginalEnvironment;
    private bool Disposed;

    public GitTestSandbox() {
        Root = Path.Combine(Path.GetTempPath(), "Brows.Git.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Marker = Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(Root, ".brows-git-test-root"), Marker, new UTF8Encoding(false));
        Hooks = Path.Combine(Root, "hooks");
        GlobalConfig = Path.Combine(Root, "global.gitconfig");
        Templates = Path.Combine(Root, "empty-template");
        Directory.CreateDirectory(Hooks);
        Directory.CreateDirectory(Templates);
        File.WriteAllText(GlobalConfig, string.Empty, new UTF8Encoding(false));
        OriginalEnvironment = IsolateProcessGitEnvironment();
    }

    public string CreateDirectory(string name) {
        string path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public string PathFor(string name) {
        return Path.Combine(Root, name);
    }

    public string InitRepository(string name, bool bare = false) {
        string path = Path.Combine(Root, name);
        List<string> arguments = new() { "-c", $"init.templateDir={Templates}", "init", "--initial-branch=main" };
        if (bare) {
            arguments.Add("--bare");
        }
        arguments.Add(path);
        RunGit(Root, arguments.ToArray());
        ConfigureRepository(path);
        return path;
    }

    public string CloneRepository(string source, string name) {
        string path = Path.Combine(Root, name);
        RunGit(Root, "clone", source, path);
        ConfigureRepository(path);
        return path;
    }

    public void ConfigureRepository(string repository) {
        RunGit(repository, "config", "user.name", "Brows Git Tests");
        RunGit(repository, "config", "user.email", "brows-git-tests@example.invalid");
        RunGit(repository, "config", "commit.gpgsign", "false");
        RunGit(repository, "config", "tag.gpgsign", "false");
        RunGit(repository, "config", "core.hooksPath", Hooks);
        RunGit(repository, "config", "core.quotepath", "false");
        RunGit(repository, "config", "status.renames", "true");
        RunGit(repository, "config", "core.autocrlf", "false");
    }

    public void WriteFile(string repository, string relativePath, string contents) {
        string filePath = Path.Combine(repository, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(filePath, contents, new UTF8Encoding(false));
    }

    public void CommitAll(string repository, string message) {
        RunGit(repository, "add", "--all");
        RunGit(repository, "commit", "--message", message);
    }

    public GitCommandResult RunGit(string workingDirectory, params string[] arguments) {
        GitCommandResult result = ExecuteGit(workingDirectory, arguments);
        if (result.ExitCode != 0) {
            throw new InvalidOperationException(
                $"git {string.Join(" ", arguments)} failed with exit code {result.ExitCode}.{Environment.NewLine}" +
                result.StandardError + result.StandardOutput);
        }
        return result;
    }

    public GitCommandResult RunGitExpectingFailure(string workingDirectory, params string[] arguments) {
        GitCommandResult result = ExecuteGit(workingDirectory, arguments);
        if (result.ExitCode == 0) {
            throw new InvalidOperationException(
                $"git {string.Join(" ", arguments)} unexpectedly succeeded.{Environment.NewLine}" +
                result.StandardOutput);
        }
        return result;
    }

    public void Dispose() {
        if (Disposed) {
            return;
        }
        Disposed = true;
        try {
            string fullRoot = Path.GetFullPath(Root);
            if ((File.GetAttributes(fullRoot) & FileAttributes.ReparsePoint) != 0) {
                throw new InvalidOperationException("Refusing to remove a temporary root that became a reparse point.");
            }
            string markerPath = Path.Combine(fullRoot, ".brows-git-test-root");
            if (!File.Exists(markerPath) || !string.Equals(File.ReadAllText(markerPath), Marker, StringComparison.Ordinal)) {
                throw new InvalidOperationException("Refusing to remove a temporary directory without its ownership marker.");
            }
            string tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Brows.Git.Tests"));
            string relative = Path.GetRelativePath(tempRoot, fullRoot);
            if (relative == "." || relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)) {
                throw new InvalidOperationException("Refusing to remove a path outside the test temporary directory.");
            }
            DeleteTree(fullRoot);
        }
        finally {
            RestoreProcessGitEnvironment(OriginalEnvironment);
        }
    }

    private GitCommandResult ExecuteGit(string workingDirectory, IReadOnlyList<string> arguments) {
        ProcessStartInfo startInfo = new("git") {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        foreach (string argument in arguments) {
            startInfo.ArgumentList.Add(argument);
        }

        RemoveInheritedGitVariable(startInfo, "GIT_DIR");
        RemoveInheritedGitVariable(startInfo, "GIT_WORK_TREE");
        RemoveInheritedGitVariable(startInfo, "GIT_COMMON_DIR");
        RemoveInheritedGitVariable(startInfo, "GIT_INDEX_FILE");
        RemoveInheritedGitVariable(startInfo, "GIT_OBJECT_DIRECTORY");
        RemoveInheritedGitVariable(startInfo, "GIT_ALTERNATE_OBJECT_DIRECTORIES");
        RemoveInheritedGitVariable(startInfo, "GIT_PREFIX");
        RemoveInheritedGitVariable(startInfo, "GIT_CONFIG_COUNT");
        RemoveInheritedGitVariable(startInfo, "GIT_CONFIG_PARAMETERS");
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_CONFIG_GLOBAL"] = GlobalConfig;
        startInfo.Environment["GIT_CONFIG_SYSTEM"] = Path.Combine(Root, "empty-system.gitconfig");
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_AUTHOR_NAME"] = "Brows Git Tests";
        startInfo.Environment["GIT_AUTHOR_EMAIL"] = "brows-git-tests@example.invalid";
        startInfo.Environment["GIT_COMMITTER_NAME"] = "Brows Git Tests";
        startInfo.Environment["GIT_COMMITTER_EMAIL"] = "brows-git-tests@example.invalid";
        startInfo.Environment["GIT_AUTHOR_DATE"] = "2001-01-01T00:00:00+00:00";
        startInfo.Environment["GIT_COMMITTER_DATE"] = "2001-01-01T00:00:00+00:00";

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start git.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(milliseconds: 30_000)) {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Task.WaitAll(standardOutput, standardError);
            throw new TimeoutException($"git {string.Join(" ", arguments)} exceeded its 30 second time limit.");
        }
        return new GitCommandResult(process.ExitCode, standardOutput.GetAwaiter().GetResult(), standardError.GetAwaiter().GetResult());
    }

    private static void RemoveInheritedGitVariable(ProcessStartInfo startInfo, string name) {
        startInfo.Environment.Remove(name);
    }

    private Dictionary<string, string> IsolateProcessGitEnvironment() {
        string[] names = [
            "GIT_CONFIG_GLOBAL", "GIT_CONFIG_SYSTEM", "GIT_CONFIG_NOSYSTEM", "GIT_CONFIG_COUNT", "GIT_CONFIG_PARAMETERS",
            "GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY",
            "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_PREFIX", "GIT_CEILING_DIRECTORIES", "GIT_NAMESPACE",
            "GIT_DISCOVERY_ACROSS_FILESYSTEM", "GIT_OPTIONAL_LOCKS", "HOME", "USERPROFILE", "XDG_CONFIG_HOME"
        ];
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in names) {
            values.Add(name, Environment.GetEnvironmentVariable(name));
        }
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", GlobalConfig);
        Environment.SetEnvironmentVariable("GIT_CONFIG_SYSTEM", Path.Combine(Root, "empty-system.gitconfig"));
        Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
        Environment.SetEnvironmentVariable("GIT_CONFIG_COUNT", null);
        Environment.SetEnvironmentVariable("GIT_CONFIG_PARAMETERS", null);
        Environment.SetEnvironmentVariable("GIT_DIR", null);
        Environment.SetEnvironmentVariable("GIT_WORK_TREE", null);
        Environment.SetEnvironmentVariable("GIT_COMMON_DIR", null);
        Environment.SetEnvironmentVariable("GIT_INDEX_FILE", null);
        Environment.SetEnvironmentVariable("GIT_OBJECT_DIRECTORY", null);
        Environment.SetEnvironmentVariable("GIT_ALTERNATE_OBJECT_DIRECTORIES", null);
        Environment.SetEnvironmentVariable("GIT_PREFIX", null);
        Environment.SetEnvironmentVariable("GIT_CEILING_DIRECTORIES", null);
        Environment.SetEnvironmentVariable("GIT_NAMESPACE", null);
        Environment.SetEnvironmentVariable("GIT_DISCOVERY_ACROSS_FILESYSTEM", null);
        Environment.SetEnvironmentVariable("GIT_OPTIONAL_LOCKS", "0");
        Environment.SetEnvironmentVariable("HOME", Root);
        Environment.SetEnvironmentVariable("USERPROFILE", Root);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Root);
        return values;
    }

    private static void RestoreProcessGitEnvironment(Dictionary<string, string> values) {
        foreach (KeyValuePair<string, string> value in values) {
            Environment.SetEnvironmentVariable(value.Key, value.Value);
        }
    }

    private static void DeleteTree(string directoryPath) {
        foreach (string childDirectory in Directory.EnumerateDirectories(directoryPath)) {
            FileAttributes attributes = File.GetAttributes(childDirectory);
            if ((attributes & FileAttributes.ReparsePoint) != 0) {
                Directory.Delete(childDirectory, recursive: false);
            }
            else {
                DeleteTree(childDirectory);
            }
        }
        foreach (string filePath in Directory.EnumerateFiles(directoryPath)) {
            NormalizeReadOnly(filePath);
            File.Delete(filePath);
        }
        NormalizeReadOnly(directoryPath);
        Directory.Delete(directoryPath, recursive: false);
    }

    private static void NormalizeReadOnly(string path) {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) == 0 && (attributes & FileAttributes.ReadOnly) != 0) {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }
}

internal sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError);
