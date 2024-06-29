using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class OpenFile : IOpenFile {
        public Task<bool> Work(string file, CancellationToken token) {
            return Task.Run(cancellationToken: token, function: () => {
                Win32File.Open(file);
                return true;
            });
        }
    }
}
