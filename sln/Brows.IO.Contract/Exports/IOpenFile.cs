using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IOpenFile : IExport {
        Task<bool> Work(string file, CancellationToken token);
    }
}
