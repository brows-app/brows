using Domore.IO;
using Domore.Logs;
using Domore.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config;

internal sealed class ConfigFileWatcher {
    private static readonly ILog Log = Logging.For(typeof(ConfigFileWatcher));
    private static readonly ConfigFileWatcher Instance = new();

    private readonly List<ConfigFileInfo> Subscribers = new();

    private TaskCache<object> Initialization => _Initialization ??= new TaskCache<object>(async token => {
        var path = await ConfigPath.FileReady(token);
        var evnt = FileSystemEventTasks.Add(path, FileSystemEvent_Handler);
        return this;
    });
    private TaskCache<object> _Initialization;

    private ConfigFileWatcher() {
    }

    private Task FileSystemEvent_Handler(FileSystemEventArgs e, CancellationToken token) {
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
        return Task.CompletedTask;
    }

    public static async Task Subscribe(ConfigFileInfo info, CancellationToken token) {
        if (Log.Info()) {
            Log.Info($"{nameof(Subscribe)} > {info?.File}");
        }
        await
        Instance.Initialization.Ready(token);
        Instance.Subscribers.Add(info);
    }
}
