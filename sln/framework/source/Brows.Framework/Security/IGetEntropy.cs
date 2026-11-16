using Brows.Composition;
using System;
using System.Security;
using System.Threading.Tasks;

namespace Brows.Security;

public interface IGetEntropy : IExport {
    Task<bool> Work(Action<SecureString> set, CancellationToken token);
}
