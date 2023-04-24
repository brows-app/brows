using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface ICaseSensitiveDirectory : IExport {
        Task<bool> Work(string directory, Action<bool> set, CancellationToken token);
    }
}
