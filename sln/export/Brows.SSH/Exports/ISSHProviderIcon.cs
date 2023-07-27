using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface ISSHProviderIcon : IExport {
        Task<bool> Work(Action<object> set, CancellationToken token);
    }
}
