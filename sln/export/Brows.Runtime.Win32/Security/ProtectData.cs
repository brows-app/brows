using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Security {
    internal sealed class ProtectData : IProtectData {
        async Task<bool> IProtectData.Work(byte[] data, byte[] entropy, Action<byte[]> complete, CancellationToken token) {
            if (complete == null) {
                return false;
            }
            var d = await Task.Run(() => ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser), token);
            complete(d);
            return true;
        }
    }
}
