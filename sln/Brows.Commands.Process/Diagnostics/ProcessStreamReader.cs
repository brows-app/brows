using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Diagnostics {
    using Gui;

    internal class ProcessStreamReader {
        private static readonly ILog Log = Logging.For(typeof(ProcessStreamReader));

        private async IAsyncEnumerable<string> ReadAll([EnumeratorCancellation] CancellationToken cancellationToken) {
            var buffer = new char[256];
            var reader = StreamReader;
            for (; ; ) {
                var s = default(string);
                try {
                    var c = cancellationToken.IsCancellationRequested;
                    if (c) {
                        break;
                    }
                    var r = await reader.ReadAsync(buffer, cancellationToken);
                    if (r > 0) {
                        s = new string(buffer, 0, r);
                        if (Log.Info()) {
                            Log.Info(s);
                        }
                    }
                }
                catch (Exception ex) {
                    if (ex is OperationCanceledException canceled && canceled.CancellationToken == cancellationToken) {
                        if (Log.Info()) {
                            Log.Info(nameof(OperationCanceledException));
                        }
                    }
                    else {
                        if (Log.Error()) {
                            Log.Error(ex);
                        }
                    }
                    break;
                }
                if (s != null) {
                    yield return s;
                }
            }
        }

        private async Task ReadLines(CancellationToken cancellationToken) {
            var all = ReadAll(cancellationToken);
            await foreach (var a in all) {
                if (a.Contains('\n')) {
                    var lines = a.Split('\n');
                    var length = lines.Length;
                    for (var i = 0; i < length; i++) {
                        if (State.LogItem == null) {
                            State.LogItem = new LogItem { Severity = Severity };
                            State.Logbook.Add(State.LogItem);
                        }
                        State.LogItem.Message += lines[i];

                        var newLine = i < (length - 1);
                        if (newLine) {
                            State.LogItem = null;
                        }
                    }
                }
                else {
                    if (State.LogItem == null) {
                        State.LogItem = new LogItem { Severity = Severity };
                        State.Logbook.Add(State.LogItem);
                    }
                    State.LogItem.Message += a;
                }
            }
        }

        public StreamReader StreamReader { get; }
        public ProcessLogState State { get; }
        public LogSeverity Severity { get; }

        public ProcessStreamReader(StreamReader streamReader, ProcessLogState state, LogSeverity severity) {
            StreamReader = streamReader ?? throw new ArgumentNullException(nameof(streamReader));
            State = state ?? throw new ArgumentNullException(nameof(state));
            Severity = severity;
        }

        public async Task Read(CancellationToken cancellationToken) {
            await ReadLines(cancellationToken);
        }
    }
}
