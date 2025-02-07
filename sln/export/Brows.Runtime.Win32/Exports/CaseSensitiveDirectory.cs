using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class CaseSensitiveDirectory : ICaseSensitiveDirectory {
        public async Task<bool> Work(string directory, Action<bool> set, CancellationToken token) {
            if (set == null) {
                return false;
            }
            var result = await Win32Path.IsCaseSensitive(directory, token);
            set(result);
            return true;
        }
    }
}
