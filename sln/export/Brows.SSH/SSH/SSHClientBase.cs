using System.Threading;
using System.Threading.Tasks;

namespace Brows.SSH {
    public abstract class SSHClientBase {
        public abstract Task CreateDirectory(string path, CancellationToken token);
        public abstract Task Delete(SSHFileInfo item, CancellationToken token);
        public abstract Task<string> CheckSum(string hash, SSHFileInfo item, CancellationToken token);
    }
}
