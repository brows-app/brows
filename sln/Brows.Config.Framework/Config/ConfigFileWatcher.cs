using Domore.IO;
using Domore.Logs;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    internal sealed class ConfigFileWatcher {
        private static readonly ILog Log = Logging.For(typeof(ConfigFileWatcher));
        private static readonly ConfigFileWatcher Instance = new();
        private readonly List<ConfigFileInfo> Subscribers = new();
        private Task ReadyTask;

        private ConfigFileWatcher() {
        }

        private async Task ReadyInit(CancellationToken cancellationToken) {
            var path = await ConfigPath.FileReady(cancellationToken);
            var evnt = new FileSystemEvent(path);
            evnt.Handler += FileSystemEvent_Handler;
        }

        private void FileSystemEvent_Handler(object sender, FileSystemEventArgs e) {
            if (Log.Info()) {
                Log.Info($"{e?.ChangeType} > {e?.FullPath}");
            }
            Subscribers.ToList().ForEach(sub => {
                var name = Path.GetFileName(sub.File);
                if (name == e?.Name) {
                    if (Log.Info()) {
                        Log.Info($"{nameof(Subscribers.Remove)} > {sub.File}");
                    }
                    sub.Invalidate();
                    Subscribers.Remove(sub);
                }
            });
        }

        private async ValueTask Ready(CancellationToken cancellationToken) {
            if (ReadyTask == null) {
                ReadyTask = ReadyInit(cancellationToken);
                try {
                    await ReadyTask;
                }
                catch {
                    ReadyTask = null;
                    throw;
                }
            }
        }

        public static async Task Subscribe(ConfigFileInfo info, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info($"{nameof(Subscribe)} > {info?.File}");
            }
            await Instance.Ready(cancellationToken);
            Instance.Subscribers.Add(info);
        }
    }
}
