using Domore.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class FileSystemEntryRefreshDelay {
        private static readonly ILog Log = Logging.For(typeof(FileSystemEntryRefreshDelay));

        public Task Completed { get; }

        public FileSystemEntryRefreshDelay(FileSystemEntry entry, int milliseconds, CancellationToken token) {
            if (null == entry) throw new ArgumentNullException(nameof(entry));
            if (Log.Debug()) {
                Log.Debug(Log.Join(nameof(Task.Delay), entry.ID));
            }
            Completed = Task.Delay(milliseconds, token).ContinueWith(task => {
                if (task.IsCompletedSuccessfully) {
                    Task.Delay(5, token).ContinueWith(task => {
                        if (task.IsCompletedSuccessfully) {
                            if (Log.Debug()) {
                                Log.Debug(Log.Join(nameof(entry.Refresh), entry.ID));
                            }
                            entry.Refresh(delayed: true);
                        }
                    });
                    if (Log.Debug()) {
                        Log.Debug(Log.Join(nameof(Completed), entry.ID));
                    }
                }
            });
        }
    }
}
