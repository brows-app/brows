using Domore.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class FileSystemEntryRefreshDelay {
        private static readonly ILog Log = Logging.For(typeof(FileSystemEntryRefreshDelay));

        private static async Task Complete(FileSystemEntry entry, int milliseconds, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(entry);
            try {
                if (Log.Debug()) {
                    Log.Debug(Log.Join(nameof(Task.Delay), entry.ID));
                }
                await Task.Delay(milliseconds, token);
                await Task.Delay(5, token);
                if (Log.Debug()) {
                    Log.Debug(Log.Join(nameof(entry.Refresh), entry.ID));
                }
                await entry.Refresh(delayed: true, token);
                if (Log.Debug()) {
                    Log.Debug(Log.Join(nameof(Completed), entry.ID));
                }
            }
            catch (OperationCanceledException ex) when (token.IsCancellationRequested) {
                if (Log.Debug()) {
                    Log.Debug(Log.Join(ex?.GetType()?.Name ?? "Canceled", entry.ID));
                }
            }
            catch (Exception ex) {
                if (Log.Warn()) {
                    Log.Warn(ex);
                }
            }
        }

        public int Milliseconds { get; }
        public Task Completed { get; }
        public FileSystemEntry Entry { get; }
        public CancellationToken Token { get; }

        public FileSystemEntryRefreshDelay(FileSystemEntry entry, int milliseconds, CancellationToken token) {
            Entry = entry;
            Token = token;
            Milliseconds = milliseconds;
            Completed = Complete(Entry, Milliseconds, Token);
        }
    }
}
