using Domore.Logs;
using Domore.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Threading.Tasks;

    internal class Win32FileLocator {
        private static readonly ILog Log = Logging.For(typeof(Win32FileLocator));
        private readonly Dictionary<string, string> Programs = new(StringComparer.OrdinalIgnoreCase);

        private string LocateInDirectory(DirectoryInfo directory, string program) {
            if (null == directory) throw new ArgumentNullException(nameof(directory));
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

        private string LocateInPath(string program) {
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

        private string LocateInPrograms(string program) {
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

        private string LocateWithWhere(string program) {
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

        private string Locate(string program) {
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

        public STAThreadPool ThreadPool { get; }

        public Win32FileLocator(STAThreadPool threadPool) {
            ThreadPool = threadPool;
        }

        public async Task<string> Program(string program, CancellationToken cancellationToken) {
            lock (Programs) {
                if (Programs.TryGetValue(program, out var value)) {
                    return value;
                }
            }
            var location = await Async.With(cancellationToken).Run(() => {
                lock (Programs) {
                    if (Programs.TryGetValue(program, out var location)) {
                        return location;
                    }
                    return Locate(program);
                }
            });
            lock (Programs) {
                Programs[program] = location;
            }
            return location;
        }
    }
}
