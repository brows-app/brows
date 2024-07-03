using Domore.Logs;
using Domore.Runtime.InteropServices;
using Domore.Runtime.InteropServices.Extensions;
using Domore.Threading;
using Domore.Threading.Tasks;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public static class Win32Overlay {
        private static readonly ILog Log = Logging.For(typeof(Win32Overlay));
        private static readonly Dictionary<string, object> OverlayCache = [];
        private static readonly STAThreadPool ThreadPool = new(nameof(Win32Overlay)) {
            TryWorkDelay = 10,
            WorkerCountMax = 1
        };

        private static TaskCache<IReadOnlyList<ShellIconOverlayIdentifierWrapper>> IdentifierCache => _IdentifierCache ??= new(IdentifiersLoad);
        private static TaskCache<IReadOnlyList<ShellIconOverlayIdentifierWrapper>> _IdentifierCache;

        private static Task<IReadOnlyList<ShellIconOverlayIdentifierWrapper>> IdentifiersLoad(CancellationToken token) {
            const string shellIconOverlayIdentifiers = @"Software\Microsoft\Windows\CurrentVersion\Explorer\ShellIconOverlayIdentifiers";
            return ThreadPool.Work(nameof(IdentifiersLoad), cancellationToken: token, work: () => {
                var list = new List<ShellIconOverlayIdentifierWrapper>();
                using (var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)) {
                    using (var subKey = hive.OpenSubKey(shellIconOverlayIdentifiers)) {
                        var names = subKey.GetSubKeyNames();
                        foreach (var name in names) {
                            if (token.IsCancellationRequested) {
                                token.ThrowIfCancellationRequested();
                            }
                            using (var key = hive.OpenSubKey($@"{shellIconOverlayIdentifiers}\{name}")) {
                                var value = key.GetValue("");
                                var parsed = Guid.TryParse(value?.ToString(), out var clsid);
                                if (parsed) {
                                    if (Log.Info()) {
                                        Log.Info(
                                            $"{nameof(IdentifiersLoad)}",
                                            $"{nameof(name)}  > {name}",
                                            $"{nameof(clsid)} > {clsid}");
                                    }
                                    list.Add(new ShellIconOverlayIdentifierWrapper(name, clsid));
                                }
                            }
                        }
                    }
                }
                return (IReadOnlyList<ShellIconOverlayIdentifierWrapper>)list;
            });
        }

        private static async Task<object> GetOverlayIcon(string path, CancellationToken token) {
            if (Log.Info()) {
                Log.Info($"{nameof(GetOverlayIcon)} > {path}");
            }
            var identifier = await IdentifierCache.Ready(token).ConfigureAwait(false);
            var allOptions = identifier.ToList();
            var overlaySrc = ThreadPool.Work(nameof(GetOverlayIcon), cancellationToken: token, work: () => {
                var members = new List<ShellIconOverlayIdentifierWrapper>();
                foreach (var option in allOptions) {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    if (option.IsMemberOf(path)) {
                        members.Add(option);
                    }
                }
                var topMember = default(ShellIconOverlayIdentifierWrapper);
                var topPriority = int.MaxValue;
                switch (members.Count) {
                    case 0:
                        if (Log.Debug()) {
                            Log.Debug(nameof(members) + " > " + members.Count);
                        }
                        break;
                    case 1:
                        if (Log.Info()) {
                            Log.Info(nameof(members) + " > " + members[0].Name);
                        }
                        topMember = members[0];
                        topPriority = 0;
                        break;
                    case > 1:
                        foreach (var member in members) {
                            if (token.IsCancellationRequested) {
                                token.ThrowIfCancellationRequested();
                            }
                            var priority = member.GetPriority();
                            if (Log.Info()) {
                                Log.Info(member.Name + " > " + priority);
                            }
                            if (topPriority > priority) {
                                topPriority = priority;
                                topMember = member;
                            }
                        }
                        break;
                }
                if (topMember != null) {
                    var key = topMember.Name;
                    lock (OverlayCache) {
                        if (OverlayCache.TryGetValue(key, out var value) == false) {
                            OverlayCache[key] = value = topMember.GetOverlaySource();
                        }
                        return value;
                    }
                }
                return null;
            });
            return await overlaySrc.ConfigureAwait(false);
        }

        public static Task<object> Load(string path, CancellationToken token) {
            return GetOverlayIcon(path, token);
        }
    }
}
