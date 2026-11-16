using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal interface ISftpFileOperations {
    Task DeleteFile(string path, CancellationToken token);
    Task DeleteDirectory(string path, CancellationToken token);
    Task Rename(string source, string destination, CancellationToken token);
}
