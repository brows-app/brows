using Domore.Logs;
using Domore.Runtime.Win32;
using Domore.Threading;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Threading.Tasks;

    internal sealed class Win32FileOpener {
        private static readonly ILog Log = Logging.For(typeof(Win32FileOpener));

        public STAThreadPool ThreadPool { get; }

        public Win32FileOpener(STAThreadPool threadPool) {
            ThreadPool = threadPool;
        }

        public async Task Open(string file, string with, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(Open) + " > " + file);
            }
            var info = new SHELLEXECUTEINFOW {
                cbSize = (uint)Marshal.SizeOf<SHELLEXECUTEINFOW>(),
                fMask = (uint)(SEE_MASK.NOASYNC | SEE_MASK.INVOKEIDLIST | SEE_MASK.FLAG_NO_UI | SEE_MASK.FLAG_LOG_USAGE),
                lpDirectory = Path.GetDirectoryName(file),
                lpFile = file,
                lpVerb = null,
                nShow = (int)SW.SHOW,
            };
            if (with != null) {
                info.lpFile = with;
                info.lpParameters = $"\"{file}\"";
            }
            var success = await Async.With(cancellationToken).Run(() => shell32.ShellExecuteExW(ref info));
            if (success == false) {
                throw new Win32Exception();
            }
        }
    }
}
