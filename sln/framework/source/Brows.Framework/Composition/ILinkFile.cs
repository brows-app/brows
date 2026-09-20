using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface ILinkFile : IExport {
    Task<bool> Work(string file, Action<string> set, CancellationToken cancellationToken);
}
