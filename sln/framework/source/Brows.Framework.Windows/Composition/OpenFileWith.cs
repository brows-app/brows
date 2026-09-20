using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed class OpenFileWith : IOpenFileWith {
    public Task<bool> Work(string file, string with, CancellationToken token) {
        return Task.Run(cancellationToken: token, function: () => {
            Win32File.Open(file, with);
            return true;
        });
    }
}
