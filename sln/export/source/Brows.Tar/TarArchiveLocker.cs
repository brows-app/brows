using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class TarArchiveLocker {
    private static readonly ILog Log = Logging.For(typeof(TarArchiveLocker));
    private static readonly Dictionary<TarArchiveIdentity, TarArchiveLocker> Set = new();
    private static readonly Lock SetLocker = new();

    private int ReferenceCount;

    private readonly TarArchiveIdentity Identity;
    private readonly SemaphoreSlim Semaphore = new(1, 1);

    private TarArchiveLocker(TarArchiveIdentity identity) {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
    }

    private TarArchiveLocker Referenced() {
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
    internal static TarArchiveLocker GetForTest(FileInfo file) {
        ArgumentNullException.ThrowIfNull(file);
        return Get(TarArchiveIdentity.CreateFallback(file));
    }

    internal static TarArchiveLocker Get(TarArchiveIdentity identity) {
        ArgumentNullException.ThrowIfNull(identity);
        lock (SetLocker) {
            if (Set.TryGetValue(identity, out var value) is false) {
                Set[identity] = value = new TarArchiveLocker(identity);
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
        private TarArchiveLocker Agent;

        public Locked(TarArchiveLocker agent) {
            Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        }

        public void Dispose() {
            var agent = Agent;
            Agent = null;
            agent?.Semaphore.Release();
        }
    }
}
