using Domore.IO;
using Domore.Logs;
using Domore.Threading.Tasks;
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

        private TaskCache<object> Initialization => _Initialization ??= new TaskCache<object>(async token => {
            var path = await ConfigPath.FileReady(token).ConfigureAwait(false);
            var evnt = new FileSystemEvent(path);
            evnt.Handler += FileSystemEvent_Handler;
            return this;
        });
        private TaskCache<object> _Initialization;

        private ConfigFileWatcher() {
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

        public static async Task Subscribe(ConfigFileInfo info, CancellationToken token) {
            if (Log.Info()) {
                Log.Info($"{nameof(Subscribe)} > {info?.File}");
            }
            await
            Instance.Initialization.Ready(token).ConfigureAwait(false);
            Instance.Subscribers.Add(info);
        }
    }
}
