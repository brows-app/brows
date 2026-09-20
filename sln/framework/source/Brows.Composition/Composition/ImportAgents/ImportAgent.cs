using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition.ImportAgents;

/// <summary>
/// Maintains the state of imports and coordinates initialization.
/// </summary>
/// <remarks>
/// An instance of this class acts as the agent for the static methods
/// of the <see cref="Imports"/> class.
/// </remarks>
internal sealed class ImportAgent : IImport {
    private static readonly ILog Log = Logging.For(typeof(ImportAgent));

    private readonly Lock ImportLocker = new();
    private readonly Queue<Action> ReadyCallbacks = [];

    private bool Ready;
    private bool Initializing;
    private ImportState ImportState;
    private Task<ImportCollection> ImportTask;

    private T UseImports<T>(bool throwIfNotReady, Func<ImportCollection, T> function, Func<T> defaultValueFactory) {
        if (function is null) {
            throw new ArgumentNullException(nameof(function));
        }
        var imports = default(ImportCollection);
        lock (ImportLocker) {
            /*
             * The imports collection is only ready if the state object
             * is ready. If the imports-ready task is complete, then we
             * can use the collection. Otherwise, we can't use it, yet.
             */
            imports = ImportTask?.Status == TaskStatus.RanToCompletion
                ? ImportTask.Result
                : null;
        }
        if (imports is not null) {
            /*
             * The imports are READY!
             */
            return function(imports);
        }
        /*
         * The imports are NOT ready.
         */
        if (throwIfNotReady) {
            throw new ImportsNotReadyException(this);
        }
        if (defaultValueFactory is null) {
            return default;
        }
        return defaultValueFactory();
    }

    private void UseImports(bool throwIfNotReady, Action<ImportCollection> action) {
        if (action is null) {
            throw new ArgumentNullException(nameof(action));
        }
        UseImports(
            throwIfNotReady: throwIfNotReady,
            defaultValueFactory: default,
            function: imports => {
                action(imports);
                return default(object);
            });
    }

    private async Task Init(ImportVariables variables,
                            ImportCollectionFactory factory,
                            CancellationToken token) {
        try {
            lock (ImportLocker) {
                /*
                 * If we're CURRENTLY initializing, throw.
                 */
                Initializing = Initializing
                    ? throw new InvalidOperationException("This method is not reentrant.")
                    : true;
                /*
                 * If we've ALREADY initialized, throw.
                 */
                if (ImportState is not null) {
                    throw new ImportsAlreadyInitializedException();
                }
                ImportState = new(this, variables, factory);
                ImportTask = ImportState.Ready(token);
            }
            await ImportTask;
            /*
             * The collection initialization is complete, so we can 
             * start processing the callback queue.
             * 
             * It is possible that a callback that invokes the imports
             * collection throws an exception related to the collection's
             * initialization if Kill is called during processing. So,
             * don't call Kill until this method is awaited.
             */
            lock (ReadyCallbacks) {
                for (Ready = true; ReadyCallbacks.Count > 0;) {
                    ReadyCallbacks.Dequeue()();
                }
            }
            /*
             * Imported types can specify callbacks to notify their
             * instances that the imports are ready. This is where
             * those callbacks are invoked.
             */
            UseImports(throwIfNotReady: false, action: imports => {
                imports.InvokeImportsReadyCallbacks();
            });
        }
        finally {
            Initializing = false;
        }
    }

    internal IImportEnvironment Environment { get; }

    internal ImportAgent(IImportEnvironment environment) {
        Environment = environment;
    }

    /// <summary>
    /// Instantiates implementations of <see cref="IExport"/> available to the
    /// extension framework and ensures any initialization logic required by
    /// each has been run.
    /// </summary>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>
    /// A task that, upon completion, signals that the imports have been
    /// initialized and are ready for use.
    /// </returns>
    public async Task Init(CancellationToken token) {
        var info = Environment?.ImportInfo;
        var vars = info?.Variables;
        var factory = info?.CollectionFactory();
        await Init(vars, factory, token);
    }

    bool IImportAgent.Ready => Ready;
    IImportEnvironment IImport.Environment => Environment;

    IReadOnlyList<T> IImportAgent.List<T>(bool throwIfNotReady) {
        return UseImports(
            function: imports => imports.List<T>(),
            throwIfNotReady: throwIfNotReady,
            /*
             * The default value, if imports are not ready,
             * is an empty list.
             */
            defaultValueFactory: () => []);
    }

    T IImportAgent.Find<T>(bool throwIfNotFound, bool throwIfNotReady) {
        return UseImports(
            throwIfNotReady: throwIfNotReady,
            defaultValueFactory: () => default,
            function: imports => {
                var import = imports.Find<T>();
                if (import == null) {
                    if (Log.Warn()) {
                        Log.Warn($"Import not found: {typeof(T)}");
                    }
                    if (throwIfNotFound) {
                        throw new ImportNotFoundException(typeof(T));
                    }
                }
                return import;
            });
    }

    void IImportAgent.Populate(object obj, bool force) {
        UseImports(
            throwIfNotReady: true,
            imports => {
                if (obj is IExport import) {
                    if (imports.Contains(import)) {
                        if (force == false) {
                            /*
                             * This import should have been populated when the imports
                             * were initialized, so we can skip the redundant population.
                             */
                            return;
                        }
                    }
                }
                imports.Populate(obj, this);
            });
    }

    T IImportAgent.Construct<T>() {
        return UseImports(
            throwIfNotReady: true,
            defaultValueFactory: default,
            function: imports => imports.Construct<T>(agent: this));
    }

    void IImport.Kill() {
        var state = default(ImportState);
        lock (ImportLocker) {
            lock (ReadyCallbacks) {
                Ready = false;
                ReadyCallbacks.Clear();
            }
            /*
             * This will reset the state of this agent. The imports
             * will have to be re-initialized before they're available
             * again.
             */
            state = ImportState;
            ImportState = null;
            ImportTask = null;
        }
        state?.Dispose();
    }

    void IImport.ReadyCallback(Action action) {
        if (action is null) {
            throw new ArgumentNullException(nameof(action));
        }
        var ready = true;
        /*
         * Queue the callback if the imports are not yet ready,
         * i.e. some initialization has not yet completed. We'll
         * dequeue it later and run it.
         */
        lock (ReadyCallbacks) {
            if (Ready == false) {
                ReadyCallbacks.Enqueue(action);
                ready = false;
            }
        }
        /*
         * Or, if the imports are ready, i.e. all initialization
         * is complete, run the callback immediately.
         */
        if (ready) {
            action();
        }
    }
}
