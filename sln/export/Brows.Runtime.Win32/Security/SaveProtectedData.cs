using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Security {
    internal sealed class SaveProtectedData : ISaveProtectedData {
        Task<bool> ISaveProtectedData.Work(IReadOnlyDictionary<string, byte[]> data, CancellationToken token) {
            if (token.IsCancellationRequested) {
                return Task.FromCanceled<bool>(token);
            }
            data = data?
                .Where(d => d.Key is not null && d.Value is not null)?
                .ToDictionary(d => d.Key, d => d.Value);
            if (data == null || data.Count == 0) {
                return Task.FromResult(false);
            }
            return Task.Run(cancellationToken: token, function: () => {
                RegistryPath.Open(security => {
                    foreach (var pair in data) {
                        var name = Convert.ToHexString(ProtectedData.Protect(Encoding.UTF8.GetBytes(pair.Key), null, DataProtectionScope.CurrentUser));
                        var value = pair.Value;
                        security.SetValue(name, value);
                    }
                });
                return true;
            });
        }
    }
}
