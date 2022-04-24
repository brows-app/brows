using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Logger;
    using Runtime.Win32;
    using Threading.Tasks;

    internal class Win32FileOpener {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(Win32FileOpener)));
        private ILog _Log;

        public async Task Open(string file, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(
                    nameof(Open),
                    $"{nameof(file)} > {file}");
            }
            var info = new SHELLEXECUTEINFOW {
                cbSize = (uint)Marshal.SizeOf<SHELLEXECUTEINFOW>(),
                fMask = (uint)(SEE_MASK.INVOKEIDLIST | SEE_MASK.FLAG_NO_UI | SEE_MASK.FLAG_LOG_USAGE),
                lpDirectory = Path.GetDirectoryName(file),
                lpFile = file,
                lpVerb = null, //"open",
                nShow = (int)SW.SHOW,
            };
            var success = await Async.Run(cancellationToken, () => shell32.ShellExecuteExW(ref info));
            if (success == false) throw new Win32Exception();
        }
    }
}
