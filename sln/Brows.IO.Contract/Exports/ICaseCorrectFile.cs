using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface ICaseCorrectFile : IExport {
        Task<bool> Work(string file, Action<string> set, CancellationToken token);
    }
}
