using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Security;

internal sealed class SecureEntropy : IGetEntropy, ISetEntropy {
    private readonly Lock Locker = new();
    private SecureString Entropy;

    void ISetEntropy.Work(SecureString entropy) {
        lock (Locker) {
            Entropy = entropy?.Copy();
        }
    }

    Task<bool> IGetEntropy.Work(Action<SecureString> set, CancellationToken token) {
        if (token.IsCancellationRequested) {
            return Task.FromCanceled<bool>(token);
        }
        var entropy = default(SecureString);
        lock (Locker) {
            entropy = Entropy?.Copy();
        }
        if (entropy is null) {
            return Task.FromResult(false);
        }
        if (set is not null) {
            set(entropy);
        }
        return Task.FromResult(true);
    }
}
