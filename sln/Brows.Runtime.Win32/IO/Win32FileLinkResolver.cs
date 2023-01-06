using Domore.Logs;
using Domore.Runtime.InteropServices;
using Domore.Threading;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    internal class Win32FileLinkResolver {
        private static readonly ILog Log = Logging.For(typeof(Win32FileLinkResolver));

        public STAThreadPool ThreadPool { get; }

        public Win32FileLinkResolver(STAThreadPool threadPool) {
            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        }

        public async Task<string> Resolve(string file, CancellationToken cancellationToken) {
            var ext = Path.GetExtension(file);
            if (ext?.EndsWith("lnk", StringComparison.OrdinalIgnoreCase) != true) {
                return null;
            }
            if (Log.Info()) {
                Log.Info(
                    nameof(Resolve),
                    $"{nameof(file)} > {file}");
            }
            try {
                return await ThreadPool.Work(
                    name: nameof(Win32FileLinkResolver),
                    cancellationToken: cancellationToken,
                    work: () => {
                        using (var wrapper = new ShellWrapper()) {
                            var shell = (dynamic)wrapper.Shell;
                            var folder = shell.NameSpace(Path.GetDirectoryName(file));
                            var folderItem = folder?.ParseName(Path.GetFileName(file));
                            var link = folderItem?.GetLink;
                            var path = link?.Path;
                            return path;
                        }
                    });
            }
            catch (Exception ex) {
                if (Log.Warn()) {
                    Log.Warn(ex);
                }
                return null;
            }
        }
    }
}
