using Brows.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO;

public interface ISelectFilesToSave : IExport {
    Task<FilesToSave> SelectFiles(FilesOptions options, CancellationToken token);
}
