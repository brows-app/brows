﻿using Domore.Logs;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public static class Win32ProgramLocator {
        private static readonly ILog Log = Logging.For(typeof(Win32ProgramLocator));
        private static readonly SemaphoreSlim CacheLocker = new(1, 1);
        private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);

        private static string LocateInDirectory(DirectoryInfo directory, string program) {
            ArgumentNullException.ThrowIfNull(directory);
            if (Log.Info()) {
                Log.Info(nameof(LocateInDirectory) + " > " + directory.FullName);
            }
            if (directory.Exists) {
                var files = directory.EnumerateFiles(
                    searchPattern: program,
                    enumerationOptions: new EnumerationOptions {
                        AttributesToSkip = (FileAttributes)0,
                        BufferSize = 0,
                        IgnoreInaccessible = true,
                        MatchCasing = MatchCasing.CaseInsensitive,
                        MatchType = MatchType.Simple,
                        MaxRecursionDepth = int.MaxValue,
                        RecurseSubdirectories = true,
                        ReturnSpecialDirectories = false
                    });
                var programFile = files.FirstOrDefault();
                if (programFile != null) {
                    return programFile.FullName;
                }
            }
            return null;
        }

        private static string LocateInPath(string program) {
            if (Log.Info()) {
                Log.Info(nameof(LocateInPath) + " > " + program);
            }
            var targets = new[] { EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine };
            foreach (var target in targets) {
                var path = Environment.GetEnvironmentVariable("PATH", target);
                var dirs = path.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var dir in dirs) {
                    var directory = new DirectoryInfo(dir);
                    var file = LocateInDirectory(directory, program);
                    if (file != null) {
                        return file;
                    }
                }
            }
            return null;
        }

        private static string LocateInPrograms(string program) {
            if (Log.Info()) {
                Log.Info(nameof(LocateInPrograms) + " > " + program);
            }
            var folders = new Environment.SpecialFolder[] {
                Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86,
                Environment.SpecialFolder.Programs,
                Environment.SpecialFolder.CommonProgramFiles,
                Environment.SpecialFolder.CommonProgramFilesX86,
                Environment.SpecialFolder.CommonPrograms
            };
            foreach (var folder in folders) {
                var dir = Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);
                var directory = new DirectoryInfo(dir);
                var file = LocateInDirectory(directory, program);
                if (file != null) {
                    return file;
                }
            }
            return null;
        }

        private static string LocateWithWhere(string program) {
            if (Log.Info()) {
                Log.Info(nameof(LocateWithWhere) + " > " + program);
            }
            var where = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System, Environment.SpecialFolderOption.DoNotVerify), "where.exe");
            var whereExists = File.Exists(where);
            if (whereExists) {
                var s = new StringBuilder();
                using (var process = new Process()) {
                    process.StartInfo.Arguments = $"$path:{program}";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.ErrorDialog = false;
                    process.StartInfo.FileName = where;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.UseShellExecute = false;
                    process.ErrorDataReceived += (_, e) => s.AppendLine(e?.Data);
                    process.OutputDataReceived += (_, e) => s.AppendLine(e?.Data);
                    process.Start();
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                    process.WaitForExit();
                    var exitCode = process.ExitCode;
                    if (exitCode == 0) {
                        return s
                            .ToString()
                            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .FirstOrDefault();
                    }
                }
            }
            return null;
        }

        private static string Locate(string program) {
            var location =
                //LocateInPath(program) ??
                LocateWithWhere(program) ??
                LocateInPrograms(program);
            if (Log.Info()) {
                Log.Info(
                    nameof(Locate),
                    nameof(program) + " > " + program,
                    nameof(location) + " > " + location);
            }
            return location;
        }

        public static async Task<string> Locate(string file, CancellationToken token) {
            if (Cache.TryGetValue(file, out var value) == false) {
                await CacheLocker.WaitAsync(token).ConfigureAwait(false);
                try {
                    if (Cache.TryGetValue(file, out value) == false) {
                        Cache[file] = value = await Task.Run(() => Locate(file), token).ConfigureAwait(false);
                    }
                }
                finally {
                    CacheLocker.Release();
                }
            }
            return value;
        }
    }
}
