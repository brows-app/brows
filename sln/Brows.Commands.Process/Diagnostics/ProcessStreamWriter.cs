using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Diagnostics {
    using Gui;
    using Logger;

    internal class ProcessStreamWriter {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(ProcessStreamWriter)));
        private ILog _Log;

        public StreamWriter StreamWriter { get; }
        public ProcessLogState State { get; }

        public ProcessStreamWriter(StreamWriter streamWriter, ProcessLogState state) {
            StreamWriter = streamWriter ?? throw new ArgumentNullException(nameof(streamWriter));
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public async Task Write(string input, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(input);
            }
            var writer = StreamWriter;
            var sb = new StringBuilder(input);
            try {
                await writer.WriteLineAsync(sb, cancellationToken);
                State.LogItem = new LogItem { Message = sb.ToString() };
                State.Logbook.Add(State.LogItem);
                State.LogItem = null;
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
            }
        }
    }
}
