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

    internal sealed class Win32FileProperties {
        private static readonly ILog Log = Logging.For(typeof(Win32FileProperties));

        public STAThreadPool ThreadPool { get; }

        public Win32FileProperties(STAThreadPool threadPool) {
            ThreadPool = threadPool;
        }

        public async Task Show(string file, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(Show) + " > " + file);
            }
            var info = new SHELLEXECUTEINFOW {
                cbSize = (uint)Marshal.SizeOf<SHELLEXECUTEINFOW>(),
                fMask = (uint)(SEE_MASK.NOASYNC | SEE_MASK.INVOKEIDLIST | SEE_MASK.FLAG_NO_UI | SEE_MASK.FLAG_LOG_USAGE),
                lpDirectory = Path.GetDirectoryName(file),
                lpFile = file,
                lpVerb = "properties",
                nShow = (int)SW.SHOW,
            };
            var success = await Async.With(cancellationToken).Run(() => shell32.ShellExecuteExW(ref info));
            if (success == false) {
                throw new Win32Exception();
            }
        }
    }
}
