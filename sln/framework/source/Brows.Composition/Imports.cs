using Brows.Composition;
using Brows.Composition.ImportAgents;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Imports implementations of <see cref="IExport"/> for the extension framework. Each implementation 
/// is created once and that instance is a long-living object.
/// </summary>
/// <remarks>
/// If an implementation supports the contract <see cref="IExportAndVary"/>, import variables are passed
/// to the instance before the instance is made available.
/// 
/// If an implementation supports the contract <see cref="IExportAndInit"/>, initialization logic is 
/// automatically run before the instance is made available. This initialization occurs after the call
/// to <see cref="IExportAndVary.Vary(IExportVariables, CancellationToken)"/>, if the implementation
/// also supports <see cref="IExportAndVary"/>.
/// 
/// If an implementation supports the contract <see cref="IExportAndKill"/>, clean-up work in the instance's
/// <see cref="IExportAndKill.Kill"/> method will run upon calling <see cref="IImport.Kill"/>.
/// </remarks>
public static class Imports {
    private static readonly IImport None = new ImportNone();
    private static readonly SemaphoreSlim Locker = new(1, 1);

    internal static async Task Reset(CancellationToken token) {
        await Locker.WaitAsync(token);
        try {
            Current?.Kill();
            Current = null;
        }
        finally {
            Locker.Release();
        }
    }

    /// <summary>
    /// Raised when the value of <see cref="Current"/> changes (including resets).
    /// </summary>
    public static event EventHandler Changed;

    /// <summary>
    /// Gets the current instance of <see cref="IImport"/> for the environment.
    /// </summary>
    public static IImport Current {
        get => field ?? None;
        private set {
            if (!Equals(field, value)) {
                field = value;
                Changed?.Invoke(sender: null, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Initializes the import container for the environment.
    /// </summary>
    /// <param name="environment">The environment of the import container.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the imports have already been initialized.
    /// </exception>
    public static async Task<IImport> Init(IImportEnvironment environment, CancellationToken token) {
        await Locker.WaitAsync(token);
        try {
            if (Current != None) {
                throw new ImportsAlreadyInitializedException();
            }
            ImportAgent agent;
            agent = new(environment);
            Current = agent;
            await agent.Init(token);
            return Current;
        }
        finally {
            Locker.Release();
        }
    }

    /// <summary>
    /// Gets a flag that indicates whether or not the imports are ready
    /// for use, i.e. whether or not they'be been initialized.
    /// </summary>
    public static bool Ready => Current.Ready;

    /// <summary>
    /// Finds the first instance of <typeparamref name="T"/> available to the extension framework.
    /// </summary>
    /// <typeparam name="T">The type of implementation of <see cref="IExport"/> to find.</typeparam>
    /// <param name="throwIfNotFound">
    /// A flag that indicates whether or not an exception is thrown when an instance of <typeparamref name="T"/>
    /// is not found. If true, an exception is thrown if no instance is found. If false, null is returned if
    /// no instance is found.
    /// </param>
    /// <param name="throwIfNotReady">
    /// A flag that indicates whether or not an exception is thrown when <see cref="Ready"/> is false.
    /// If true, an exception is thrown if <see cref="Ready"/> is false.
    /// If false, and <see cref="Ready"/> is false, the default value of <typeparamref name="T"/>
    /// is returned.
    /// </param>
    /// <returns>
    /// The first instance of <typeparamref name="T"/> found by the framework,
    /// or null if no instance is found and <paramref name="throwIfNotFound"/> is false,
    /// or if <see cref="Ready"/> is false and <paramref name="throwIfNotReady"/> is false.
    /// </returns>
    /// <exception cref="ImportNotFoundException">
    /// Thrown if no instance of <typeparamref name="T"/> is found and <paramref name="throwIfNotFound"/> is true.
    /// </exception>
    public static T Find<T>(bool throwIfNotFound = false, bool throwIfNotReady = true) where T : IExport {
        return Current.Find<T>(throwIfNotFound: throwIfNotFound, throwIfNotReady: throwIfNotReady);
    }

    /// <summary>
    /// Gets the collection of instances of type <typeparamref name="T"/> available to the extension framework.
    /// </summary>
    /// <typeparam name="T">The type of implementation of <see cref="IExport"/> that the list contains.</typeparam>
    /// <returns>
    /// The list of imported instances. If no instances are available, the list contains zero items.
    /// </returns>
    public static IReadOnlyList<T> List<T>(bool throwIfNotReady = true) where T : IExport {
        return Current.List<T>(throwIfNotReady: throwIfNotReady);
    }
}
