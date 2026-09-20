using Brows.Composition;
using Domore.IO;
using Domore.Logs;
using System.Threading.Tasks;

namespace Brows;

internal static class LogFileSystemEvents {
    private sealed class FileSystemEventLogger : IExport, IExportAndInit {
        private static readonly ILog Log = Logging.For(typeof(FileSystemEventLogger));

        Task IExportAndInit.Init(IExportContext context, CancellationToken token) {
            FileSystemEventTasks.OnManagerCanceled = (result, token) => {
                return Task.CompletedTask;
            };
            FileSystemEventTasks.OnManagerError = (result, token) => {
                Log.Error(result?.Exception);
                return Task.CompletedTask;
            };
            FileSystemEventTasks.OnSubscriptionEventCanceled = (result, token) => {
                return Task.CompletedTask;
            };
            FileSystemEventTasks.OnSubscriptionEventComplete = (result, token) => {
                return Task.CompletedTask;
            };
            FileSystemEventTasks.OnSubscriptionEventError = (result, token) => {
                Log.Error(result?.Exception);
                return Task.CompletedTask;
            };
            FileSystemEventTasks.OnUnhandledError = (exception, token) => {
                Log.Error(exception);
                return Task.FromResult(false);
            };
            return Task.CompletedTask;
        }
    }
}
