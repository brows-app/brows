using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface ICaseSensitiveDrives : IExport {
        Task<bool> CaseSensitive(Drives drives, CancellationToken cancellationToken);
    }
}
