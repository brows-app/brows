using Domore.Logs;
using Domore.Runtime.InteropServices;
using Domore.Runtime.InteropServices.Extensions;
using Domore.Threading;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Gui {
    internal sealed class Win32OverlayProvider : OverlayProvider {
        private static readonly ILog Log = Logging.For(typeof(Win32OverlayProvider));

        private readonly ConcurrentDictionary<string, object> OverlayCache = new ConcurrentDictionary<string, object>();
        private readonly ConcurrentDictionary<string, Task<object>> OverlayTasks = new ConcurrentDictionary<string, Task<object>>();

        private IReadOnlyList<ShellIconOverlayIdentifierWrapper> Identifiers;
        private Task<IReadOnlyList<ShellIconOverlayIdentifierWrapper>> IdentifiersTask;

        private async Task<IReadOnlyList<ShellIconOverlayIdentifierWrapper>> IdentifiersInit() {
            const string shellIconOverlayIdentifiers = @"Software\Microsoft\Windows\CurrentVersion\Explorer\ShellIconOverlayIdentifiers";
            return await ThreadPool.Work(nameof(IdentifiersInit), cancellationToken: CancellationToken.None, work: () => {
                var list = new List<ShellIconOverlayIdentifierWrapper>();
                using (var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)) {
                    using (var subKey = hive.OpenSubKey(shellIconOverlayIdentifiers)) {
                        var names = subKey.GetSubKeyNames();
                        foreach (var name in names) {
                            using (var key = hive.OpenSubKey($@"{shellIconOverlayIdentifiers}\{name}")) {
                                var value = key.GetValue("");
                                var parsed = Guid.TryParse(value?.ToString(), out var clsid);
                                if (parsed) {
                                    if (Log.Info()) {
                                        Log.Info(
                                            $"{nameof(IdentifiersInit)}",
                                            $"{nameof(name)}  > {name}",
                                            $"{nameof(clsid)} > {clsid}");
                                    }
                                    list.Add(new ShellIconOverlayIdentifierWrapper(name, clsid));
                                }
                            }
                        }
                    }
                }
                return list;
            });
        }

        private async ValueTask<IReadOnlyList<ShellIconOverlayIdentifierWrapper>> GetIdentifiers() {
            if (Identifiers == null) {
                if (IdentifiersTask == null) {
                    IdentifiersTask = IdentifiersInit();
                }
                Identifiers = await IdentifiersTask;
            }
            return Identifiers;
        }

        private async Task<object> GetOverlayIcon(string path, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info($"{nameof(GetOverlayIcon)} > {path}");
            }
            var allOptions = await GetIdentifiers();
            var overlaySrc = await ThreadPool.Work(nameof(GetOverlayIcon), cancellationToken: cancellationToken, work: () => {
                var memberOption = allOptions.Select(i => i.IsMemberOf(path) ? i : null);
                var memberOptions = memberOption.Where(i => i != null).ToList();
                cancellationToken.ThrowIfCancellationRequested();

                var topOption = default(ShellIconOverlayIdentifierWrapper);
                var topPriority = int.MaxValue;
                switch (memberOptions.Count) {
                    case 0:
                        if (Log.Debug()) {
                            Log.Debug(nameof(memberOptions) + " > " + memberOptions.Count);
                        }
                        break;
                    case 1:
                        if (Log.Info()) {
                            Log.Info(nameof(memberOptions) + " > " + memberOptions[0].Name);
                        }
                        topOption = memberOptions[0];
                        topPriority = 0;
                        break;
                    case > 1:
                        var priorities = memberOptions.Select(i => (Option: i, Priority: i.GetPriority()));
                        foreach (var item in priorities) {
                            if (Log.Info()) {
                                Log.Info(item.Option.Name + " > " + item.Priority);
                            }
                            if (item.Priority < topPriority) {
                                topOption = item.Option;
                                topPriority = item.Priority;
                            }
                        }
                        break;
                }
                cancellationToken.ThrowIfCancellationRequested();

                if (topOption != null) {
                    var key = topOption.Name;
                    var cache = OverlayCache;
                    if (cache.TryGetValue(key, out var value) == false) {
                        cache[key] = value = topOption.GetOverlaySource();
                    }
                    return value;
                }
                return null;
            });
            return overlaySrc;
        }

        public STAThreadPool ThreadPool { get; }

        public Win32OverlayProvider(STAThreadPool threadPool) {
            //ThreadPool = threadPool;
            ThreadPool = new STAThreadPool(nameof(Win32OverlayProvider)) {
                TryWorkDelay = 10,
                WorkerCountMax = 1
            };
        }

        public sealed override Task<object> GetImageSource(IOverlayInput input, ImageSize size, CancellationToken cancellationToken) {
            if (null == input) throw new ArgumentNullException(nameof(input));
            return GetOverlayIcon(input.ID, cancellationToken);
        }
    }
}
