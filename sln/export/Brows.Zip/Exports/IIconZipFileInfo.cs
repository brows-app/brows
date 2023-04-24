using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IIconZipFileInfo : IExport {
        Task<object> Icon(FileInfo zipFileInfo, CancellationToken cancellationToken);
    }
}
