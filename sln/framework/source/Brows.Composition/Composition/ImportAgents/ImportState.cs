using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition.ImportAgents;

/// <summary>
/// This type's job is to initialize a collection of imports, i.e. an
/// instance of <see cref="ImportCollection"/>. It is intended to be
/// thread-safe for collection initialization and retrieval.
/// </summary>
internal sealed class ImportState : IDisposable {
    private static readonly ILog Log = Logging.For(typeof(ImportState));

    private readonly Lock Locker = new();

    private bool Disposed;
    private ImportCollection CollectionCache;
    private Task<ImportCollection> CollectionTask;

    private ObjectDisposedException DisposedException() =>
        new(GetType().Name);

    private void Dispose(bool disposing) {
        if (disposing) {
            var killItems = default(IReadOnlyList<IExportAndKill>);
            var resources = default(IReadOnlyList<IDisposable>);
            lock (Locker) {
                /*
                 * Flag that this instance is disposed. This flag must
                 * never get reset to false. This is a one-and-done instance.
                 */
                Disposed = true;
                killItems = CollectionCache?.List<IExportAndKill>();
                resources = CollectionCache?.Resources?.Reverse()?.ToList();
            }
            if (killItems is not null && killItems.Count > 0) {
                foreach (var killItem in killItems) {
                    try {
                        killItem?.Kill();
                    }
                    catch (Exception ex) {
                        /*
                         * Don't let Dispose throw any exceptions.
                         */
                        Log.Error(ex);
                    }
                }
            }
            if (resources is not null && resources.Count > 0) {
                foreach (var resource in resources) {
                    try {
                        resource?.Dispose();
                    }
                    catch (Exception ex) {
                        /*
                         * Don't let Dispose throw any exceptions.
                         */
                        Log.Error(ex);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the variables passed to the initialization of the imports.
    /// </summary>
    public ImportVariables Variables { get; }

    /// <summary>
    /// Gets the factory object that creates the instance of <see cref="ImportCollection"/>.
    /// </summary>
    public ImportCollectionFactory CollectionFactory { get; }


    /// <summary>
    /// Gets the import agent.
    /// </summary>
    public IImportAgent Agent { get; }

    public ImportState(IImportAgent agent, ImportVariables variables, ImportCollectionFactory collectionFactory) {
        Agent = agent;
        Variables = variables;
        CollectionFactory = collectionFactory;
    }

    /// <summary>
    /// Creates, initializes, and caches an instance of <see cref="ImportCollection"/>.
    /// </summary>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>
    /// A task that, when complete, results in the cached instance of the collection of
    /// imports. The same instance is returned on subsequent calls.
    /// </returns>
    public async Task<ImportCollection> Ready(CancellationToken token) {
        if (CollectionCache is not null) {
            return CollectionCache;
        }
        if (CollectionTask is null) {
            lock (Locker) {
                if (Disposed) {
                    throw DisposedException();
                }
                if (CollectionTask is null) {
                    var collectionTask = ImportCollection.Init(
                        agent: Agent,
                        factory: CollectionFactory,
                        variables: Variables,
                        token: token);
                    Thread.MemoryBarrier(); // Required for double-checked locking
                    CollectionTask = collectionTask;
                }
            }
        }
        return CollectionCache = await CollectionTask;
    }

    /// <summary>
    /// Releases resources used by the instance.
    /// </summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~ImportState() {
        Dispose(false);
    }
}
