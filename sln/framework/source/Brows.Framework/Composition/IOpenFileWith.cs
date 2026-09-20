using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface IOpenFileWith : IExport {
    Task<bool> Work(string file, string with, CancellationToken token);
}
