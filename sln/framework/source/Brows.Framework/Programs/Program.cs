using Brows.Composition;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using IMPORTS = Brows.Imports;

namespace Brows.Programs;

public abstract class Program : IProgram, IImportEnvironment {
    private readonly Lazy<DisposableImports> LazyImports;

    protected virtual string Name =>
        GetType().Name;

    protected virtual ImportInfo ImportInfo => ImportInfo.Composed(
        path: () => new Dictionary<string, IEnumerable<string>> {
            { ".", ["Brows.*.dll"] },
            { "brows.export", ["Brows.*.dll"] },
        },
        variables: () => new());

    protected Program() {
        LazyImports = new(() => new(this));
    }

    protected abstract Task<int> Run(IProgramContext context, CancellationToken token);

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

public abstract class Program<TConfig> : Program where TConfig : new() {
    protected abstract Task<int> Run(TConfig config, IProgramContext context, CancellationToken token);

    protected sealed override Task<int> Run(IProgramContext context, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(context);
        var config = context.Configure(new TConfig());
        return Run(config, context, token);
    }
}
