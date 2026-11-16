using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SevenZipArchiveLocker {
    private static readonly ILog Log = Logging.For(typeof(SevenZipArchiveLocker));
    private static readonly Dictionary<SevenZipArchiveIdentity, SevenZipArchiveLocker> Set = new();
    private static readonly Lock SetLocker = new();

    private int ReferenceCount;

    private readonly SevenZipArchiveIdentity Identity;
    private readonly SemaphoreSlim Semaphore = new(1, 1);

    private SevenZipArchiveLocker(SevenZipArchiveIdentity identity) {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
    }

    private SevenZipArchiveLocker Referenced() {
        if (Log.Info()) {
            Log.Info(nameof(Referenced) + " > " + Identity.FullName + " > " + ReferenceCount);
        }
        ReferenceCount++;
        return this;
    }

    /*
     * Tests use this fallback when no composition scope exists. Runtime
     * lockers share the identity resolved for their archive path.
     */
    internal static SevenZipArchiveLocker GetForTest(FileInfo file) {
        ArgumentNullException.ThrowIfNull(file);
        return Get(SevenZipArchiveIdentity.CreateFallback(file));
    }

    internal static SevenZipArchiveLocker Get(SevenZipArchiveIdentity identity) {
        ArgumentNullException.ThrowIfNull(identity);
        lock (SetLocker) {
            if (Set.TryGetValue(identity, out var value) is false) {
                Set[identity] = value = new SevenZipArchiveLocker(identity);
            }
            return value.Referenced();
        }
    }

    public IDisposable Lock() {
        Semaphore.Wait();
        return new Locked(this);
    }

    public async Task<IDisposable> Lock(CancellationToken cancellationToken) {
        await Semaphore.WaitAsync(cancellationToken);
        return new Locked(this);
    }

    public void Release() {
        lock (SetLocker) {
            if (Log.Info()) {
                Log.Info(nameof(Release) + " > " + Identity.FullName);
            }
            var count = --ReferenceCount;
            if (count == 0) {
                if (Log.Info()) {
                    Log.Info(nameof(Set.Remove));
                }
                Set.Remove(Identity);
                ThreadPool.QueueUserWorkItem(_ => {
                    try {
                        Semaphore.Wait();
                        Semaphore.Dispose();
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

    private sealed class Locked : IDisposable {
        private SevenZipArchiveLocker Agent;

        public Locked(SevenZipArchiveLocker agent) {
            Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        }

        public void Dispose() {
            var agent = Agent;
            Agent = null;
            agent?.Semaphore.Release();
        }
    }
}
