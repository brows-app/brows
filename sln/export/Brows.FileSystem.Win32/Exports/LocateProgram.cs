using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class LocateProgram : ILocateProgram {
        public async Task<bool> Work(string program, Action<string> set, CancellationToken token) {
            if (set == null) {
                return false;
            }
            var result = await Win32ProgramLocator.Locate(program, token).ConfigureAwait(false);
            if (result == null) {
                return false;
            }
            set(result);
            return true;
        }
    }
}
