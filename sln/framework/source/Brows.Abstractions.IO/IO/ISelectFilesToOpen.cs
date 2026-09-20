using Brows.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO;

public interface ISelectFilesToOpen : IExport {
    Task<FilesToOpen> SelectFiles(FilesOptions options, CancellationToken token);
}
