using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class OpenFile : IOpenFile {
        public async Task<bool> Work(string file, CancellationToken token) {
            await Task.Run(cancellationToken: token, action: () => {
                Win32File.Open(file);
            });
            return true;
        }
    }
}
