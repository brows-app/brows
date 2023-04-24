using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class OpenFileWith : IOpenFileWith {
        public async Task<bool> Work(string file, string with, CancellationToken token) {
            await Task.Run(cancellationToken: token, action: () => {
                Win32File.Open(file, with);
            });
            return true;
        }
    }
}
