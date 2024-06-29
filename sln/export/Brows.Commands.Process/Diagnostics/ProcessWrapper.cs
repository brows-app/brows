using Brows.Exports;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Diagnostics {
    internal sealed class ProcessWrapper : Notifier {
        private static readonly ILog Log = Logging.For(typeof(ProcessWrapper));

        private Process Process;

        public int? ProcessID {
            get => _ProcessID;
            private set => Change(ref _ProcessID, value, nameof(ProcessID));
        }
        private int? _ProcessID;

        public DateTime? ExitTime {
            get => _ExitTime;
            private set => Change(ref _ExitTime, value, nameof(ExitTime));
        }
        private DateTime? _ExitTime;

        public DateTime? StartTime {
            get => _StartTime;
            private set => Change(ref _StartTime, value, nameof(StartTime));
        }
        private DateTime? _StartTime;

        public int? ExitCode {
            get => _ExitCode;
            private set => Change(ref _ExitCode, value, nameof(ExitCode));
        }
        private int? _ExitCode;

        public bool Started {
            get => _Started;
            private set => Change(ref _Started, value, nameof(Started));
        }
        private bool _Started;

        public bool Running {
            get => _Running;
            private set => Change(ref _Running, value, nameof(Running));
        }
        private bool _Running;

        public object Stream {
            get => _Stream;
            private set => Change(ref _Stream, value, nameof(Stream));
        }
        private object _Stream;

        public string Input { get; }
        public string WorkingDirectory { get; }
        public IFixProcessStartInfoError Fix { get; }
        public IReadOnlyDictionary<string, string> Environment { get; }

        public ProcessWrapper(string input, string workingDirectory, IReadOnlyDictionary<string, string> environment, IFixProcessStartInfoError fix) {
            Fix = fix;
            Input = input;
            Environment = environment;
            WorkingDirectory = workingDirectory;
        }

        public void Kill() {
            var process = Process;
            if (process != null) {
                if (Log.Info()) {
                    Log.Info(
                        nameof(Kill),
                        nameof(ProcessID) + " > " + ProcessID);
                }
                try {
                    process.Kill();
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
            }
        }

        public async Task Start(CancellationToken token) {
            if (Started == false) {
                Started = true;
                StartTime = DateTime.Now;
            }
            else {
                throw new InvalidOperationException(message: $"{nameof(Started)} [{Started}]");
            }
            if (Log.Info()) {
                Log.Info(
                    nameof(Start),
                    $"{nameof(Input)} > {Input}",
                    $"{nameof(WorkingDirectory)} > {WorkingDirectory}");
            }
            var trim = Input?.Trim() ?? "";
            var parts = trim.Split(new char[] { }, 2, StringSplitOptions.None);
            var fileName = parts[0];
            var arguments = parts.Length > 1 ? parts[1] : "";
            var startInfo = new ProcessStartInfo {
                Arguments = arguments,
                CreateNoWindow = true,
                ErrorDialog = false,
                FileName = fileName,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = WorkingDirectory
            };
            using (var process = Process = new Process()) {
                process.StartInfo = startInfo;
                process.StartInfo.Environment(Environment);
                try {
                    try {
                        process.Start();
                        Running = true;
                    }
                    catch (Exception ex) {
                        var fix = Fix;
                        if (fix == null) {
                            throw;
                        }
                        var fixd = await fix.Work(process.StartInfo, ex, token).ConfigureAwait(false);
                        if (fixd == false) {
                            throw;
                        }
                    }
                    if (Running == false) {
                        process.Start();
                        Running = true;
                    }
                    ProcessID = process.Id;
                    using (var processStream = new ProcessStream(process)) {
                        Stream = processStream;
                        await
                        process.WaitForExitAsync(token).ConfigureAwait(false);
                        processStream.Complete();
                        await processStream.Task.ConfigureAwait(false);
                    }
                    try {
                        ExitCode = process.ExitCode;
                        ExitTime = process.ExitTime;
                    }
                    catch (Exception ex) {
                        if (Log.Warn()) {
                            Log.Warn(ex);
                        }
                    }
                    if (Log.Info()) {
                        Log.Info(
                            nameof(process.Exited),
                            $"{nameof(Input)} > {Input}",
                            $"{nameof(WorkingDirectory)} > {WorkingDirectory}",
                            $"{nameof(ExitTime)} > {ExitTime}",
                            $"{nameof(ExitCode)} > {ExitCode}");
                    }
                }
                finally {
                    Process = null;
                    Running = false;
                }
            }
        }
    }
}
