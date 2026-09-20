using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface IDriveIcon : IExport {
    Task<bool> Work(DriveInfo info, Action<object> set, CancellationToken token);
}
