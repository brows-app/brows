using System;
using System.Threading.Tasks;

namespace Brows.Exports;

public interface ILocateProgram : IExport {
    Task<bool> Work(string program, Action<string> set, CancellationToken token);
}
