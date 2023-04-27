using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IFixProcessError : IExport {
        Task<bool> Work(ProcessStartInfo startInfo, Exception error, CancellationToken token);
    }
}
