using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Zip {
    internal sealed class ArchiveLocker {
        private static readonly ILog Log = Logging.For(typeof(ArchiveLocker));
        private static readonly Dictionary<string, ArchiveLocker> Set = new();

        private int ReferenceCount;

        private readonly string Key;
        private readonly SemaphoreSlim Locker = new(1, 1);

        private ArchiveLocker(string key) {
            Key = key;
        }

        private ArchiveLocker Referenced() {
            if (Log.Info()) {
                Log.Info(nameof(Referenced) + " > " + Key + " > " + ReferenceCount);
            }
            ReferenceCount++;
            return this;
        }

        public ArchiveLocked Lock() {
            Locker.Wait();
            return new Locked(this);
        }

        public async Task<ArchiveLocked> Lock(CancellationToken cancellationToken) {
            await Locker
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return new Locked(this);
        }

        public void Release() {
            lock (Set) {
                if (Log.Info()) {
                    Log.Info(nameof(Release) + " > " + Key);
                }
                var count = --ReferenceCount;
                if (count == 0) {
                    if (Log.Info()) {
                        Log.Info(nameof(Set.Remove));
                    }
                    Set.Remove(Key);
                    ThreadPool.QueueUserWorkItem(_ => {
                        try {
                            Locker.Wait();
                            Locker.Dispose();
                        }
                        catch (Exception ex) {
                            if (Log.Error()) {
                                Log.Error(ex);
                            }
                        }
                    });
                }
                if (Log.Info()) {
                    Log.Info(nameof(ReferenceCount) + " > " + ReferenceCount);
                }
            }
        }

        public static ArchiveLocker Get(FileInfo file) {
            ArgumentNullException.ThrowIfNull(file);
            var key = file.FullName;
            lock (Set) {
                if (Set.TryGetValue(key, out var value) == false) {
                    Set[key] = value = new ArchiveLocker(key);
                }
                return value.Referenced();
            }
        }

        private sealed class Locked : ArchiveLocked {
            protected sealed override void Dispose(bool disposing) {
                if (disposing) {
                    Agent.Locker.Release();
                }
                base.Dispose(disposing);
            }

            public ArchiveLocker Agent { get; }

            public Locked(ArchiveLocker agent) {
                Agent = agent ?? throw new ArgumentNullException(nameof(agent));
            }
        }
    }
}
