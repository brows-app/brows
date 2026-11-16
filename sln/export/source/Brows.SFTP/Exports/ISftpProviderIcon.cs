using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports;

internal interface ISftpProviderIcon : IExport {
    Task<bool> Work(Action<object> set, CancellationToken token);
}
