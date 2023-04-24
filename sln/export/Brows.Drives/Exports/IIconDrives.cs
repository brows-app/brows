using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IIconDrives : IExport {
        Task<object> Icon(Drives drives, CancellationToken cancellationToken);
    }
}
