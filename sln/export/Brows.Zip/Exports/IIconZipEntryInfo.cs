using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IIconZipEntryInfo : IExport {
        Task<object> Icon(ZipEntryInfo zipEntryInfo, CancellationToken cancellationToken);
    }
}
