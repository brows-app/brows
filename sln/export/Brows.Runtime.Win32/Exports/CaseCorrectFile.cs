using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class CaseCorrectFile : ICaseCorrectFile {
        public async Task<bool> Work(string file, Action<string> set, CancellationToken token) {
            if (set == null) {
                return false;
            }
            var result = await Win32Path.GetCasing(file, token);
            set(result);
            return true;
        }
    }
}
