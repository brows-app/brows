using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Gui {
    using Runtime.InteropServices;
    using Threading;
    using Threading.Tasks;

    internal class Win32OverlayProvider : OverlayProvider {
        private readonly ConcurrentDictionary<string, object> OverlayCache = new ConcurrentDictionary<string, object>();
        private readonly ConcurrentDictionary<string, Task<object>> OverlayTasks = new ConcurrentDictionary<string, Task<object>>();

        private List<ShellIconOverlayIdentifierWrapper> Identifiers;
        private Task<List<ShellIconOverlayIdentifierWrapper>> IdentifiersTask;

        private StaThreadPool ThreadPool =>
            _ThreadPool ?? (
            _ThreadPool = new StaThreadPool(nameof(Win32OverlayProvider)) { WorkerCountMax = 1 });
        private StaThreadPool _ThreadPool;

        private async Task<List<ShellIconOverlayIdentifierWrapper>> InitIdentifiers() {
            const string shellIconOverlayIdentifiers = @"Software\Microsoft\Windows\CurrentVersion\Explorer\ShellIconOverlayIdentifiers";
            return await Async.Run(CancellationToken.None, () => {
                var list = new List<ShellIconOverlayIdentifierWrapper>();
                using (var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)) {
                    using (var subKey = hive.OpenSubKey(shellIconOverlayIdentifiers)) {
                        var names = subKey.GetSubKeyNames();
                        foreach (var name in names) {
                            using (var key = hive.OpenSubKey($@"{shellIconOverlayIdentifiers}\{name}")) {
                                var value = key.GetValue("");
                                var parsed = Guid.TryParse(value?.ToString(), out var clsid);
                                if (parsed) {
                                    list.Add(new ShellIconOverlayIdentifierWrapper(name, clsid, ThreadPool));
                                }
                            }
                        }
                    }
                }
                return list;
            });
        }

        private async Task<List<ShellIconOverlayIdentifierWrapper>> GetIdentifiers() {
            if (Identifiers != null) return Identifiers;
            if (IdentifiersTask != null) return Identifiers = await IdentifiersTask;
            return Identifiers = await (IdentifiersTask = InitIdentifiers());
        }

        private async Task<object> GetOverlayIcon(string path, CancellationToken cancellationToken) {
            var allOptions = await GetIdentifiers();
            var memberOptions = new List<ShellIconOverlayIdentifierWrapper>(1);
            foreach (var option in allOptions) {
                var member = await option.IsMemberOf(path, cancellationToken);
                if (member) {
                    memberOptions.Add(option);
                }
            }
            var topOption = default(ShellIconOverlayIdentifierWrapper);
            var topPriority = int.MaxValue;
            if (memberOptions.Count == 1) {
                topOption = memberOptions[0];
                topPriority = 0;
            }
            if (memberOptions.Count > 1) {
                foreach (var option in memberOptions) {
                    var priority = await option.GetPriority(cancellationToken);
                    if (priority < topPriority) {
                        topOption = option;
                        topPriority = priority;
                    }
                }
            }
            if (topOption != null) {
                var key = topOption.Name;
                var cache = OverlayCache;
                if (cache.TryGetValue(key, out var value) == false) {
                    var tasks = OverlayTasks;
                    if (tasks.TryGetValue(key, out var task) == false) {
                        tasks[key] = task = topOption.GetOverlaySource(cancellationToken);
                    }
                    cache[key] = value = await task;
                }
                return value;
            }
            return null;
        }

        public override Task<object> GetImageSource(IOverlayInput input, ImageSize size, CancellationToken cancellationToken) {
            if (null == input) throw new ArgumentNullException(nameof(input));
            return GetOverlayIcon(input.ID, cancellationToken);
        }
    }
}
