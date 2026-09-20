using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface IOpenFile : IExport {
    Task<bool> Work(string file, CancellationToken token);
}
