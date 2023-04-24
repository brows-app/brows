using Domore.Notification;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TASK = System.Threading.Tasks.Task;

namespace Brows.Diagnostics {
    internal sealed class ProcessStream : Notifier, IDisposable {
        private readonly CancellationTokenSource TokenSource;
        private readonly ObservableCollection<ProcessStreamItem> Collection = new();

        private async Task Read(StreamReader reader, ProcessOutputKind kind, CancellationToken token) {
            if (null == reader) throw new ArgumentNullException(nameof(reader));
            var buffer = new char[1];
            for (; ; ) {
                var r = default(int);
                try {
                    r = await reader.ReadAsync(buffer, token);
                }
                catch (OperationCanceledException canceled) when (canceled.CancellationToken == token) {
                    break;
                }
                if (r > 0) {
                    var c = buffer[0];
                    switch (c) {
                        case '\r':
                            continue;
                        case '\n':
                            Collection.Add(CurrentItem);
                            CurrentItem = new ProcessStreamOutput(kind);
                            break;
                        default:
                            if (CurrentItem is ProcessStreamOutput output && output.Kind == kind) {
                                CurrentItem.Append(c);
                            }
                            else {
                                CurrentItem = new ProcessStreamOutput(kind);
                                CurrentItem.Append(c);
                            }
                            break;
                    }
                }
            }
        }

        public object Items =>
            Collection;

        public ProcessStreamItem CurrentItem {
            get => _CurrentItem;
            private set => Change(ref _CurrentItem, value, nameof(CurrentItem));
        }
        private ProcessStreamItem _CurrentItem;

        public Task Task { get; }
        public Process Process { get; }

        public ProcessStream(Process process) {
            Process = process;
            TokenSource = new();
            Task = TASK.WhenAll(
                Read(Process.StandardError, ProcessOutputKind.StandardError, TokenSource.Token),
                Read(Process.StandardOutput, ProcessOutputKind.StandardOutput, TokenSource.Token));
        }

        public void Complete() {
            TokenSource.Cancel();
        }

        void IDisposable.Dispose() {
            using (TokenSource) {
            }
        }
    }
}
