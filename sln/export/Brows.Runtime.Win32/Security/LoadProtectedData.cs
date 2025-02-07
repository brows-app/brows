using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Security {
    internal sealed class LoadProtectedData : ILoadProtectedData {
        Task<bool> ILoadProtectedData.Work(Action<IReadOnlyDictionary<string, byte[]>> set, CancellationToken token) {
            if (token.IsCancellationRequested) {
                return Task.FromCanceled<bool>(token);
            }
            if (set is null) {
                return Task.FromResult(false);
            }
            return Task.Run(cancellationToken: token, function: () => {
                RegistryPath.Open(security => {
                    var names = security.GetValueNames();
                    var result = new Dictionary<string, byte[]>(capacity: names.Length);
                    foreach (var name in names) {
                        var n = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromHexString(name), null, DataProtectionScope.CurrentUser));
                        var v = (byte[])security.GetValue(name);
                        result[n] = v;
                    }
                    set(result);
                });
                return true;
            });
        }
    }
}
