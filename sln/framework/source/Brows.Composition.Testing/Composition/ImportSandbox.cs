using Domore.Logs;
using Brows.IO;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

public sealed class ImportSandbox : IImportSandbox {
    private static readonly SemaphoreSlim Locker = new(1, 1);

    private readonly Func<IExport[]> Inject;
    private readonly Func<ImportVariables> Variables;
    private readonly FileSysSandbox FileSysSandbox;

    private ImportSandbox(Func<IExport[]> inject, Func<ImportVariables> variables) {
        Inject = inject;
        Variables = variables;
        FileSysSandbox = new();
    }

    private void Dispose(bool disposing) {
        if (disposing) {
            FileSysSandbox.Dispose();
        }
    }

    private static void Logging_Event(object sender, LogEventArgs e) {
        var messages = e?.LogList;
        if (messages is not null) {
            Debug.WriteLine(string.Join(Environment.NewLine, messages));
        }
    }

    private static async Task Lock(Func<Task> function) {
        await Locker.WaitAsync();
        try {
            await Imports.Reset(default);
            Logging.Event -= Logging_Event;
            Logging.Event += Logging_Event;
            Logging.EventThreshold = LogSeverity.Debug;
            var task = function?.Invoke();
            if (task != null) {
                await task;
            }
        }
        finally {
            Locker.Release();
        }
    }

    public static IImportSandbox Create(Func<ImportVariables> variables, Func<IExport[]> inject) {
        return new ImportSandbox(inject, variables);
    }

    public static IImportSandbox Create(Func<IExport[]> inject) {
        return Create(variables: null, inject: inject);
    }

    public static Task Clean(Action action) {
        return Lock(() => {
            action?.Invoke();
            return Task.CompletedTask;
        });
    }

    public static Task Clean(Func<Task> function) {
        return Lock(async () => {
            await (function?.Invoke() ?? Task.CompletedTask);
        });
    }

    public static Task Enter(IExport[] inject, ImportVariables variables, Action action) {
        return Enter(inject, variables, () => {
            action?.Invoke();
            return Task.CompletedTask;
        });
    }

    public static Task Enter(IExport[] inject, Action action) {
        return Enter(inject, variables: null, action);
    }

    public static Task Enter(IExport[] inject, ImportVariables variables, Func<Task> function) {
        return Lock(async () => {
            await Imports.Init(
                token: default,
                environment: new ImportSandboxEnvironment {
                    ImportInfo = ImportInfo.Listed(() => variables, () => inject)
                });
            await (function?.Invoke() ?? Task.CompletedTask);
        });
    }

    public static Task Enter(IExport[] inject, Func<Task> function) {
        return Enter(inject, variables: null, function);
    }

    IImport IImportSandbox.Imports => Imports.Current;
    string IImportSandbox.SandboxDirectory => FileSysSandbox.Root;

    Task IImportSandbox.Enter(Action<IImportSandbox> action) {
        return Enter(Inject?.Invoke(), Variables?.Invoke(), () => {
            action?.Invoke(this);
        });
    }

    Task IImportSandbox.Enter(Func<IImportSandbox, Task> function) {
        return Lock(async () => {
            await Imports.Init(
                token: default,
                environment: new ImportSandboxEnvironment {
                    ImportInfo = ImportInfo.Listed(Variables, Inject)
                });
            await (function?.Invoke(this) ?? Task.CompletedTask);
        });
    }

    async Task<T> IImportSandbox.EnterAndFind<T>() {
        var self = (IImportSandbox)this;
        var result = default(T);
        await self.Enter(sandbox => {
            result = sandbox.Imports.Find<T>();
        });
        return result;
    }

    void IDisposable.Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~ImportSandbox() {
        Dispose(false);
    }
}
