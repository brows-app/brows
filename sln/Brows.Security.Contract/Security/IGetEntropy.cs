using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Security {
    public interface IGetEntropy {
        Task<bool> Work(Action<SecureString> set, CancellationToken token);
    }
}
