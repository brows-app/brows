using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class CaseSensitiveDrives : ICaseSensitiveDrives {
        public Task<bool> CaseSensitive(Drives drives, CancellationToken cancellationToken) {
            return Task.FromResult(false);
        }
    }
}
