using Domore.Logs;
using Domore.Runtime.InteropServices;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public static class Win32FileLink {
        private static readonly ILog Log = Logging.For(typeof(Win32FileLink));

        public static async Task<string> Resolve(string file, CancellationToken cancellationToken) {
            var ext = Path.GetExtension(file);
            if (ext?.EndsWith("lnk", StringComparison.OrdinalIgnoreCase) != true) {
                return null;
            }
            if (Log.Info()) {
                Log.Info(nameof(Resolve) + " > " + file);
            }
            try {
                var work = Win32ThreadPool.Common.Work(
                    name: nameof(Win32FileLink),
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
                return await work;
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
