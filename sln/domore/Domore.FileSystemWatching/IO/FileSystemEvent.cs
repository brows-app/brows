using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PATH = System.IO.Path;

namespace Domore.IO {
    public sealed class FileSystemEvent {
        private static readonly ILog Log = Logging.For(typeof(FileSystemEvent));
        private static readonly Dictionary<string, string> Keys = [];
        private static readonly Dictionary<string, FileSystemEventPath> Cache = [];
        private static readonly FileSystemPath FileSystemPath = new();
        private static readonly char[] FileSystemTrimChars = [PATH.DirectorySeparatorChar, PATH.AltDirectorySeparatorChar];

        private static string Key(string path) {
            ArgumentNullException.ThrowIfNull(path);
            lock (Keys) {
                if (Keys.TryGetValue(path, out var key) == false) {
                    Keys[path] = key =
                        (FileSystemPath.IsCaseSensitive(path) ? path : path.ToUpper())
                        .TrimEnd(FileSystemTrimChars);
                    if (Log.Info()) {
                        Log.Info($"{nameof(Key)}[{key}]");
                    }
                }
                return key;
            }
        }

        private static void Add(string path, FileSystemEventHandler handler) {
            var key = Key(path);
            lock (Cache) {
                if (Cache.TryGetValue(key, out var item) == false) {
                    Cache[key] = item = new FileSystemEventPath(path) {
                        SynchronizationContext = SynchronizationContext
                    };
                    if (Log.Info()) {
                        Log.Info($"{nameof(Add)}[{path}]");
                    }
                }
                item.Add(handler);
            }
        }

        private static void Remove(string path, FileSystemEventHandler handler) {
            var key = Key(path);
            lock (Cache) {
                if (Cache.TryGetValue(key, out var item)) {
                    var remove = item.Remove(handler);
                    if (remove) {
                        var state = item.RemoveState = new object();
                        ThreadPool.QueueUserWorkItem(_ => {
                            Task.Delay(1000).ContinueWith(_ => {
                                lock (Cache) {
                                    if (item.Remove()) {
                                        if (item.RemoveState == state) {
                                            item.Removed();
                                            if (Log.Info()) {
                                                Log.Info($"{nameof(Remove)}[{path}]");
                                            }
                                            Cache.Remove(key);
                                        }
                                    }
                                }
                            });
                        });
                    }
                }
            }
        }

        public static SynchronizationContext SynchronizationContext {
            get => _SynchronizationContext;
            set {
                if (_SynchronizationContext != value) {
                    lock (Cache) {
                        if (_SynchronizationContext != value) {
                            _SynchronizationContext = value;
                            foreach (var path in Cache.Values) {
                                path.SynchronizationContext = value;
                            }
                        }
                    }
                }
            }
        }
        private static volatile SynchronizationContext _SynchronizationContext;

        public string Path { get; }

        public event FileSystemEventHandler Handler {
            add => Add(Path, value);
            remove => Remove(Path, value);
        }

        public FileSystemEvent(string path) {
            Path = path;
        }
    }
}
