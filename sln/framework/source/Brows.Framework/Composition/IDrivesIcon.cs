using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface IDrivesIcon : IExport {
    Task<bool> Work(IDrives drives, Action<object> set, CancellationToken token);
}
