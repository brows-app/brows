using System;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface IImportSandbox : IDisposable {
    IImport Imports { get; }
    string SandboxDirectory { get; }

    Task Enter(Action<IImportSandbox> action);
    Task Enter(Func<IImportSandbox, Task> function);

    Task<T> EnterAndFind<T>() where T : IExport;
}
