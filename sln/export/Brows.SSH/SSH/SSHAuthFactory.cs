using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.SSH {
    internal sealed class SSHAuthFactory {
        public async Task<SSHAuth> Auth(CancellationToken token) {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
            var ssh = Path.Combine(home, ".ssh");
            await Task.Run(cancellationToken: token, function: () => {
                var sshDir = new DirectoryInfo(ssh);
                if (sshDir.Exists == false) {

                }
                var keyFiles = sshDir.EnumerateFiles();
                foreach (var keyFile in keyFiles) {

                }
                throw new NotImplementedException();
            });
            throw new NotImplementedException();
        }
    }
}
