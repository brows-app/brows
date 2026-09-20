using Domore.Logs;
using Brows.Composition;
using Brows.Composition.Exports;
using Brows.Composition.ImportCollections;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// A container for instances of <see cref="IExport"/>.
/// </summary>
internal sealed class ImportCollection {
    private static readonly ILog Log = Logging.For(typeof(ImportCollection));
    private static readonly ConcurrentDictionary<Type, ImportPopulator> Populator = [];

    private readonly Lock InitializationLocker = new();
    private readonly IReadOnlyList<IExport> Agent;
    private readonly Dictionary<Type, IList> ListCache = [];
    private readonly Dictionary<Type, object> FindCache = [];
    private readonly ConcurrentDictionary<Type, object> FastCache = [];

    private IExportVariables Variables;
    private Task<ImportCollection> Initialization;

    private static async Task InitializeCore(IReadOnlyList<IExport> imports, IExportVariables variables, CancellationToken token) {
        if (imports is null) {
            throw new ArgumentNullException(nameof(imports));
        }
        if (variables is not null) {
            /////////////////////////////////////////////////
            // Supply variables to instances that accept them,
            /////////////////////////////////////////////////
            var varying = imports.OfType<IExportAndVary>();
            await Task.WhenAll(varying.Select(async import => {
                if (Log.Info()) {
                    Log.Info($"Vary import: {import.GetType()?.Name}");
                }
                var varyTask = import.Vary(variables, token);
                if (varyTask != null) {
                    await varyTask;
                }
            }));
            /////////////////////////////////////////////////////////////
            // Supply variables to instances that accept them generically
            /////////////////////////////////////////////////////////////
            await Task.WhenAll(variables.Keys
                .Select(key => new {
                    VariableType = key,
                    VariableValue = variables.Get(key)
                })
                .Where(variable => variable.VariableValue is not null)
                .Select(variable => new {
                    ExportType = typeof(IExportAndVary<>).MakeGenericType(variable.VariableType),
                    variable.VariableValue
                })
                .SelectMany(item => imports
                    .Where(import => item.ExportType.IsAssignableFrom(import.GetType()))
                    .Select(import => {
                        var varyMethod = item.ExportType.GetMethod(nameof(IExportAndVary<>.Vary));
                        return varyMethod.Invoke(import, [item.VariableValue, token]) as Task;
                    }))
                .Where(task => task is not null));
            /////////////////////////////////////////////////////////////
        }
        ////////////////////////////////////////////////////
        // Initialize instances that support initialization.
        ////////////////////////////////////////////////////
        var initing = imports.OfType<IExportAndInit>().ToList();
        var readied = imports.Except(initing).ToList();
        var context = new ImportContext(readied, syncRoot: readied);
        await Task.WhenAll(initing.Select(async import => {
            if (Log.Info()) {
                Log.Info($"Ready import: {import.GetType()?.Name}");
            }
            var initTask = import.Init(context, token);
            if (initTask != null) {
                await initTask;
            }
            /*
             * See the syncRoot parameter to the import
             * context constructor above.
             * 
             * This collection is locked to synchronize
             * reads and writes.
             */
            lock (readied) {
                readied.Add(import);
            }
        }));
        ////////////////////////////////////////////////////
    }

    private async Task<ImportCollection> InitializationTask(CancellationToken token) {
        lock (Agent) {
            if (Initializing) {
                if (Log.Warn()) {
                    Log.Warn("Already initializing!");
                }
                return this;
            }
            Initializing = true;
        }
        try {
            if (Log.Info()) {
                Log.Info("Initializing...");
            }
            await InitializeCore(
                imports: Snapshot(),
                variables: Variables,
                token: token);
        }
        finally {
            Initializing = false;
        }
        return this;
    }

    private IReadOnlyList<IExport> Snapshot() {
        IReadOnlyList<IExport> imports;
        lock (Agent) {
            imports = [.. Agent];
        }
        return imports;
    }

    /// <summary>
    /// Gets a flag that is true during initialization of the instances
    /// of <see cref="IExportAndInit"/>, or false otherwise.
    /// </summary>
    internal bool Initializing { get; private set; }

    /// <summary>
    /// Gets a collection of <see cref="IDisposable"/> instances that should be disposed when
    /// the current instance of <see cref="ImportCollection"/> is no longer needed.
    /// </summary>
    internal IEnumerable<IDisposable> Resources { get; }

    /// <summary>
    /// Creates the container with the specified items.
    /// </summary>
    /// <param name="items">
    /// The instances of <see cref="IExport"/> in the container.
    /// </param>
    /// <param name="resources">
    /// A collection of <see cref="IDisposable"/> instances that should be disposed when
    /// the current instance of <see cref="ImportCollection"/> is no longer needed.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is null.</exception>
    internal ImportCollection(IEnumerable<IExport> items, IEnumerable<IDisposable> resources = null) {
        if (null == items) throw new ArgumentNullException(nameof(items));
        Agent = [.. items.Where(item => item is not null)];
        Resources = resources;
    }

    internal static async Task<ImportCollection> Init(IImportAgent agent,
                                                      ImportCollectionFactory factory,
                                                      ImportVariables variables,
                                                      CancellationToken token) {
        if (factory is null) {
            factory = new ComposedImportCollectionFactory(new());
        }
        var
        collection = await factory.Create(token);
        collection.Variables = variables?.ExportVariables;
        await
        collection.Initialize(token);
        collection.Populate(agent);
        return collection;
    }

    internal void InvokeImportsReadyCallbacks() {
        var items = Snapshot();
        foreach (var item in items) {
            var itemType = item.GetType();
            var readyCallbacks = ExportExtension.GetImportsReadyCallbacks(itemType);
            foreach (var readyCallback in readyCallbacks) {
                readyCallback.Invoke(item, null);
            }
        }
    }

    /// <summary>
    /// Initializes instances of <see cref="IExportAndInit"/> in the container.
    /// Initialization occurs once, and, after completion, future calls to this
    /// method result in a no-op.
    /// </summary>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that, upon completion, signals that initialization is complete.</returns>
    public async Task Initialize(CancellationToken token) {
        if (Initialization is null) {
            lock (InitializationLocker) {
                if (Initialization is null) {
                    var initialization = InitializationTask(token);
                    Thread.MemoryBarrier(); // Required for double-checked locking
                    Initialization = initialization;
                }
            }
        }
        await Initialization;
    }

    /// <summary>
    /// Gets a list of instances derived from type <typeparamref name="T"/> in the container.
    /// </summary>
    /// <typeparam name="T">The type of instances in the list.</typeparam>
    /// <returns>A list of instances of type <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown during initialization.</exception>
    public IReadOnlyList<T> List<T>() where T : IExport {
        if (Initializing) {
            throw new InvalidOperationException("The imports are initializing.");
        }
        var type = typeof(T);
        lock (ListCache) {
            if (ListCache.TryGetValue(type, out var value) == false) {
                if (Log.Info()) {
                    Log.Info($"Listing: {type}");
                }
                lock (Agent) {
                    ListCache[type] = value = Agent
                        .OfType<T>()
                        .ToList()
                        .AsReadOnly();
                }
                if (Log.Info()) {
                    Log.Info($"Count: {value.Count}");
                }
            }
            return (IReadOnlyList<T>)value;
        }
    }

    /// <summary>
    /// Gets the first instance of type <typeparamref name="T"/> found in the container.
    /// </summary>
    /// <typeparam name="T">The type of the instance to find.</typeparam>
    /// <returns>The first instance of type <typeparamref name="T"/> found, or null if no instance was found.</returns>
    public T Find<T>() where T : IExport {
        var type = typeof(T);
        var fast = FastCache;
        if (fast.TryGetValue(type, out var value)) {
            return (T)value;
        }
        lock (FindCache) {
            if (FindCache.TryGetValue(type, out value) == false) {
                if (Log.Info()) {
                    Log.Info($"Finding: {type}");
                }
                FindCache[type] = value = List<T>()
                    .FirstOrDefault();
                if (Log.Info()) {
                    Log.Info($"Found: {value}");
                }
            }
            return (T)(fast[type] = value);
        }
    }

    /// <summary>
    /// Determines whether or not the specified instance exists as an import in the collection.
    /// </summary>
    /// <param name="import">The instance to check for existence in the collection.</param>
    /// <returns>True if the instance exists in the collection. Otherwise, false.</returns>
    public bool Contains(IExport import) {
        lock (Agent) {
            return Agent.Contains(import);
        }
    }

    /// <summary>
    /// Populates an object's imports.
    /// </summary>
    /// <param name="obj">The object whose imports should be populated.</param>
    /// <param name="agent">The import agent.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="obj"/> is null.</exception>
    public void Populate(object obj, IImportAgent agent) {
        if (null == obj) throw new ArgumentNullException(nameof(obj));
        var
        populator = Populator.GetOrAdd(obj.GetType(), type => new(type));
        populator.Populate(target: obj, from: this, agent: agent);
    }

    /// <summary>
    /// Populates the imports of all items in the collection.
    /// </summary>
    public void Populate(IImportAgent agent) {
        var items = Snapshot();
        foreach (var item in items) {
            Populate(item, agent);
        }
    }

    /// <summary>
    /// Creates a populated instance of a type.
    /// </summary>
    /// <typeparam name="T">The type of object to create.</typeparam>
    /// <param name="agent">The import agent.</param>
    /// <returns>The constructed instance.</returns>
    public T Construct<T>(IImportAgent agent) {
        var type = typeof(T);
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var binder = new ImportBinder(Snapshot());
        var constructors = type.GetConstructors(flags);
        var constructor = constructors
            .Select(constructor => new {
                Constructor = constructor,
                Parameters = constructor.GetParameters()
            })
            .Select(item => new {
                item.Constructor,
                item.Parameters,
                Resolution = item.Parameters
                    .Select(parameter => new {
                        Parameter = parameter,
                        Import = binder.Bind(parameter.ParameterType)
                    })
                    .ToList()
            })
            .Select(item => new {
                item.Constructor,
                item.Parameters,
                item.Resolution,
                Resolved = item.Resolution
                    .Where(i => i.Import is not null)
                    .ToList()
            })
            .OrderBy(item => item.Parameters.Length - item.Resolved.Count)
            .ThenByDescending(item => item.Resolved.Count)
            .FirstOrDefault();
        if (constructor is null) {
            return default;
        }
        var instance = constructor.Constructor.Invoke([.. constructor.Resolution.Select(r => r.Import)]);
        Populate(instance, agent);
        return (T)instance;
    }
}
