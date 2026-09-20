using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface IFixProcessStartInfoError : IExport {
    Task<bool> Work(ProcessStartInfo startInfo, Exception error, CancellationToken token);
}
