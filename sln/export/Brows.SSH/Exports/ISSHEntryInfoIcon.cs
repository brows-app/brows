using Brows.SSH;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface ISSHEntryInfoIcon : IExport {
        Task<bool> Work(SSHFileInfo sshEntryInfo, Action<object> set, CancellationToken token);
    }
}
