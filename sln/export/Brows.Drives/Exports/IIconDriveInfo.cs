using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IIconDriveInfo : IExport {
        Task<object> Icon(DriveInfo driveInfo, CancellationToken cancellationToken);
    }
}
