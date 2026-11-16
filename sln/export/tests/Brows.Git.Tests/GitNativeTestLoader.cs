using Brows.Git;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Brows;

internal static class GitNativeTestLoader {
    private const string NativeDll = "brows_git.dll";
    private const string FrameworkDll = "brows_framework.dll";
    private static readonly object Locker = new();
    private static Task LoadTask;
    private static IntPtr FrameworkHandle;
    private static IntPtr GitHandle;
    private static string PreviousPath;
    private static Dictionary<string, string> OriginalGitEnvironment;
    private static string NativeConfigurationDirectory;
    private static bool Initialized;

    public static Task Load(CancellationToken token) {
        lock (Locker) {
            return LoadTask ??= LoadCore(token);
        }
    }

    public static void Unload() {
        lock (Locker) {
            try {
                if (Initialized) {
                    NativeMethod exit = Marshal.GetDelegateForFunctionPointer<NativeMethod>(NativeLibrary.GetExport(GitHandle, "brows_git_exit"));
                    int result = exit();
                    if (result != 0) {
                        throw new InvalidOperationException($"brows_git_exit returned error {result}.");
                    }
                    Initialized = false;
                }
            }
            finally {
                if (GitHandle != IntPtr.Zero) {
                    NativeLibrary.Free(GitHandle);
                    GitHandle = IntPtr.Zero;
                }
                if (FrameworkHandle != IntPtr.Zero) {
                    NativeLibrary.Free(FrameworkHandle);
                    FrameworkHandle = IntPtr.Zero;
                }
                if (PreviousPath != null) {
                    Environment.SetEnvironmentVariable("PATH", PreviousPath);
                    PreviousPath = null;
                }
                if (OriginalGitEnvironment != null) {
                    RestoreGitEnvironment(OriginalGitEnvironment);
                    OriginalGitEnvironment = null;
                }
                if (NativeConfigurationDirectory != null && Directory.Exists(NativeConfigurationDirectory)) {
                    File.Delete(Path.Combine(NativeConfigurationDirectory, "global.gitconfig"));
                    File.Delete(Path.Combine(NativeConfigurationDirectory, "system.gitconfig"));
                    Directory.Delete(NativeConfigurationDirectory, recursive: false);
                    NativeConfigurationDirectory = null;
                }
            }
        }
    }

    private static async Task LoadCore(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64) {
            throw new PlatformNotSupportedException("Brows.Git integration tests require the x64-windows native binaries.");
        }

        try {
            IsolateGitEnvironment();
            string nativeDirectory = FindNativeDirectory();
            string frameworkPath = Path.Combine(nativeDirectory, FrameworkDll);
            string gitPath = Path.Combine(nativeDirectory, NativeDll);
            if (!File.Exists(frameworkPath) || !File.Exists(gitPath)) {
                throw new FileNotFoundException(
                    $"The native directory '{nativeDirectory}' must contain both {FrameworkDll} and {NativeDll}.");
            }

            PreviousPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            Environment.SetEnvironmentVariable("PATH", nativeDirectory + Path.PathSeparator + PreviousPath);
            FrameworkHandle = NativeLibrary.Load(frameworkPath);
            GitHandle = NativeLibrary.Load(gitPath);
            NativeLibrary.SetDllImportResolver(typeof(GitRepo).Assembly, ResolveNativeLibrary);

            NativeMethod initialize = Marshal.GetDelegateForFunctionPointer<NativeMethod>(NativeLibrary.GetExport(GitHandle, "brows_git_init"));
            int result = initialize();
            if (result != 0) {
                throw new InvalidOperationException($"brows_git_init returned error {result}.");
            }
            Initialized = true;
            await Task.CompletedTask;
        }
        catch {
            Unload();
            throw;
        }
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) {
        return string.Equals(libraryName, NativeDll, StringComparison.OrdinalIgnoreCase)
            ? GitHandle
            : IntPtr.Zero;
    }

    private static string FindNativeDirectory() {
        string environmentPath = Environment.GetEnvironmentVariable("BROWS_GIT_NATIVE_DIR");
        if (!string.IsNullOrWhiteSpace(environmentPath)) {
            string resolved = ResolveEnvironmentPath(environmentPath);
            if (ContainsRequiredFiles(resolved)) {
                return Path.GetFullPath(resolved);
            }
            throw new FileNotFoundException(
                $"BROWS_GIT_NATIVE_DIR points to '{resolved}', which does not contain {NativeDll} and {FrameworkDll}.");
        }

        string processPath = Environment.ProcessPath;
        string[] startingPaths = [
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            processPath == null ? AppContext.BaseDirectory : Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory
        ];
        foreach (string startingPath in startingPaths) {
            DirectoryInfo directory = new(Path.GetFullPath(startingPath));
            while (directory != null) {
                string output = Path.Combine(directory.FullName, "out");
                foreach (string configuration in new[] { "Debug", "Release" }) {
                    string candidate = Path.Combine(output, configuration, "brows.native", "x64-windows");
                    if (ContainsRequiredFiles(candidate)) {
                        return candidate;
                    }
                }
                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException(
            $"Could not find {NativeDll} and {FrameworkDll}. Build the native target into " +
            "sln/out/<Configuration>/brows.native/x64-windows or set BROWS_GIT_NATIVE_DIR to that directory. " +
            "Git integration tests do not skip when native binaries are absent.");
    }

    private static string ResolveEnvironmentPath(string path) {
        string fullPath = Path.GetFullPath(path);
        if (File.Exists(Path.Combine(fullPath, NativeDll))) {
            return fullPath;
        }
        return Path.Combine(fullPath, "brows.native", "x64-windows");
    }

    private static bool ContainsRequiredFiles(string path) {
        return Directory.Exists(path) &&
            File.Exists(Path.Combine(path, NativeDll)) &&
            File.Exists(Path.Combine(path, FrameworkDll));
    }

    private static void IsolateGitEnvironment() {
        string[] names = [
            "GIT_CONFIG_GLOBAL", "GIT_CONFIG_SYSTEM", "GIT_CONFIG_NOSYSTEM", "GIT_CONFIG_COUNT", "GIT_CONFIG_PARAMETERS",
            "GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY",
            "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_PREFIX", "GIT_CEILING_DIRECTORIES", "GIT_NAMESPACE",
            "GIT_DISCOVERY_ACROSS_FILESYSTEM", "GIT_OPTIONAL_LOCKS", "HOME", "USERPROFILE", "XDG_CONFIG_HOME"
        ];
        OriginalGitEnvironment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in names) {
            OriginalGitEnvironment.Add(name, Environment.GetEnvironmentVariable(name));
        }

        NativeConfigurationDirectory = Path.Combine(Path.GetTempPath(), "Brows.Git.Tests.Native", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(NativeConfigurationDirectory);
        string globalConfig = Path.Combine(NativeConfigurationDirectory, "global.gitconfig");
        string systemConfig = Path.Combine(NativeConfigurationDirectory, "system.gitconfig");
        File.WriteAllText(globalConfig, string.Empty);
        File.WriteAllText(systemConfig, string.Empty);
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", globalConfig);
        Environment.SetEnvironmentVariable("GIT_CONFIG_SYSTEM", systemConfig);
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
        Environment.SetEnvironmentVariable("HOME", NativeConfigurationDirectory);
        Environment.SetEnvironmentVariable("USERPROFILE", NativeConfigurationDirectory);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", NativeConfigurationDirectory);
    }

    private static void RestoreGitEnvironment(Dictionary<string, string> values) {
        foreach (KeyValuePair<string, string> value in values) {
            Environment.SetEnvironmentVariable(value.Key, value.Value);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NativeMethod();
}
