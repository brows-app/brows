using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Security {
    public interface ILoadProtectedData : IExport {
        Task<bool> Work(Action<IReadOnlyDictionary<string, byte[]>> set, CancellationToken token);
    }
}
