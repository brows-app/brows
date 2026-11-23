using Brows.Composition;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using IMPORTS = Brows.Imports;

namespace Brows.Programs;

/// <summary>
/// Provides a base for programs that discover and use Brows services.
/// </summary>
public abstract class Program : IProgram, IImportEnvironment {
    private readonly Lazy<DisposableImports> LazyImports;

    /// <summary>
    /// Gets the program's name; by default, the name of its type.
    /// </summary>
    protected virtual string Name =>
        GetType().Name;

    /// <summary>
    /// Gets the sources and settings used to discover the program's services.
    /// </summary>
    protected virtual ImportInfo ImportInfo => ImportInfo.Composed(
        path: () => new Dictionary<string, IEnumerable<string>> {
            { ".", ["Brows.*.dll"] },
            { "brows.export", ["Brows.*.dll"] },
        },
        variables: () => new());

    /// <summary>
    /// Initializes a program.
    /// </summary>
    protected Program() {
        LazyImports = new(() => new(this));
    }

    /// <summary>
    /// Runs the program with the supplied startup context.
    /// </summary>
    /// <param name="context">
    /// The startup arguments and configuration source.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The program exit code.
    /// </returns>
    protected abstract Task<int> Run(IProgramContext context, CancellationToken token);

    /// <summary>
    /// Initializes the program services and returns their lifetime.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// An object to dispose when the program no longer needs its services.
    /// </returns>
    protected async Task<IDisposable> Imports(CancellationToken token) {
        var imports = LazyImports.Value;
        await imports.Ready(token);
        return imports;
    }

    string IProgram.Name =>
        Name;

    async Task<int> IProgram.Run(IProgramContext context, CancellationToken token) {
        return await Run(context, token);
    }

    ImportInfo IImportEnvironment.ImportInfo => ImportInfo;

    private sealed class DisposableImports(Program program) : IDisposable {
        private readonly Lock Locker = new();
        private readonly Program Program = program;

        private bool Disposed;
        private Task<IImport> Imported;

        public async Task Ready(CancellationToken token) {
            Task<IImport> imported;
            lock (Locker) {
                ObjectDisposedException.ThrowIf(Disposed, this);
                imported = Imported ??= IMPORTS.Init(Program, token);
            }
            await imported;
        }

        void IDisposable.Dispose() {
            Task<IImport> imported;
            lock (Locker) {
                if (Disposed) {
                    return;
                }
                Disposed = true;
                imported = Imported;
                Imported = null;
            }
            if (imported is not null) {
                /*
                 * Init may still be in flight; killing it has to wait for the import to
                 * exist, and blocking here would stall (or deadlock) the disposing thread.
                 * A faulted or canceled init has nothing to kill.
                 */
                _ = imported.ContinueWith(
                    continuationAction: task => task.Result.Kill(),
                    cancellationToken: CancellationToken.None,
                    continuationOptions: TaskContinuationOptions.OnlyOnRanToCompletion |
                                         TaskContinuationOptions.ExecuteSynchronously,
                    scheduler: TaskScheduler.Default);
            }
        }
    }
}

/// <summary>
/// Runs a program with configuration populated from its startup context.
/// </summary>
/// <typeparam name="TConfig">
/// The program configuration type.
/// </typeparam>
public abstract class Program<TConfig> : Program where TConfig : new() {
    /// <summary>
    /// Runs the program with the supplied startup context.
    /// </summary>
    /// <param name="config">
    /// The populated program configuration.
    /// </param>
    /// <param name="context">
    /// The startup arguments and configuration source.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The program exit code.
    /// </returns>
    protected abstract Task<int> Run(TConfig config, IProgramContext context, CancellationToken token);

    /// <summary>
    /// Runs the program with the supplied startup context.
    /// </summary>
    /// <param name="context">
    /// The startup arguments and configuration source.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The program exit code.
    /// </returns>
    protected sealed override Task<int> Run(IProgramContext context, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(context);
        var config = context.Configure(new TConfig());
        return Run(config, context, token);
    }
}
