using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Diagnostics {
    using ComponentModel;
    using Gui;
    using Logger;
    using Threading.Tasks;

    internal class ProcessWrapper : NotifyPropertyChanged {
        private ProcessStreamWriter Writer;

        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(ProcessWrapper)));
        private ILog _Log;

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<ProcessWrapper>());
        private TaskHandler _TaskHandler;

        public Logbook Logbook =>
            _Logbook ?? (
            _Logbook = new Logbook());
        private Logbook _Logbook;

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

        public string Input { get; }
        public string WorkingDirectory { get; }

        public ProcessWrapper(string input, string workingDirectory) {
            Input = input;
            WorkingDirectory = workingDirectory;
        }

        public async Task Write(string input, CancellationToken cancellationToken) {
            var writer = Writer;
            if (writer != null) {
                await writer.Write(input, cancellationToken);
            }
        }

        public async Task Run(CancellationToken cancellationToken) {
            if (Started == false) {
                Started = true;
                StartTime = DateTime.Now;
            }
            else {
                throw new InvalidOperationException(message: $"{nameof(Started)} [{Started}]");
            }
            if (Log.Info()) {
                Log.Info(
                    nameof(Run),
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
            using (var process = new Process()) {
                process.StartInfo = startInfo;
                try {
                    await Async.Run(cancellationToken, process.Start);
                    Running = true;
                    ProcessID = process.Id;

                    var state = new ProcessLogState(Logbook);
                    var writer = new ProcessStreamWriter(process.StandardInput, state);
                    var stdErr = new ProcessStreamReader(process.StandardError, state, LogSeverity.Error);
                    var stdOut = new ProcessStreamReader(process.StandardOutput, state, LogSeverity.Info);
                    var stdErrTask = stdErr.Read(cancellationToken);
                    var stdOutTask = stdOut.Read(cancellationToken);

                    Writer = writer;
                    await process.WaitForExitAsync(cancellationToken);
                    await stdErrTask;
                    await stdOutTask;
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
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
                finally {
                    Writer = null;
                    Running = false;
                }
            }
        }
    }
}
