using Brows.Composition;
using Domore.IO;
using Domore.Logs;
using System.Threading.Tasks;

namespace Brows;

internal static class LogFileSystemEvents {
    private sealed class FileSystemEventLogger : IExport, IExportAndInit {
        private static readonly ILog Log = Logging.For(typeof(FileSystemEventLogger));

        Task IExportAndInit.Init(IExportContext context, CancellationToken token) {
            FileSystemEventTasks.OnManagerCanceled += (s, e) => {
            };
            FileSystemEventTasks.OnManagerError += (s, e) => {
                e?.Add((result, ct) => {
                    Log.Error(result?.Exception);
                    return Task.CompletedTask;
                });
            };
            FileSystemEventTasks.OnSubscriptionEventCanceled += (s, e) => {
            };
            FileSystemEventTasks.OnSubscriptionEventComplete += (s, e) => {
            };
            FileSystemEventTasks.OnSubscriptionEventError += (s, e) => {
                e?.Add((result, ct) => {
                    Log.Error(result?.Exception);
                    return Task.CompletedTask;
                });
            };
            FileSystemEventTasks.OnUnhandledError += (s, e) => {
                e?.Add((exception, ct) => {
                    Log.Error(exception);
                    return Task.FromResult(false);
                });
            };
            return Task.CompletedTask;
        }
    }
}
