using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Security {
    internal sealed class UnprotectData : IUnprotectData {
        async Task<bool> IUnprotectData.Work(byte[] data, byte[] entropy, Action<byte[]> complete, CancellationToken token) {
            if (complete == null) {
                return false;
            }
            var d = await Task.Run(() => ProtectedData.Unprotect(data, entropy, DataProtectionScope.CurrentUser), token);
            complete(d);
            return true;
        }
    }
}
