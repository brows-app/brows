using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Brows.Runtime.InteropServices {
    using ComTypes;
    using Logger;
    using Runtime.Win32;
    using Threading;

    internal sealed class ShellIconOverlayIdentifierWrapper : ComObjectWrapper<IShellIconOverlayIdentifier> {
        protected sealed override IShellIconOverlayIdentifier Factory() {
            return Identifier;
        }

        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(ShellIconOverlayIdentifierWrapper)));
        private ILog _Log;

        public IShellIconOverlayIdentifier Identifier { get; private set; }

        public string Name { get; }
        public Guid CLSID { get; }
        public StaThreadPool ThreadPool { get; }

        public ShellIconOverlayIdentifierWrapper(string name, Guid clsid, StaThreadPool threadPool) {
            Name = name;
            CLSID = clsid;
            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        }

        private async Task<IShellIconOverlayIdentifier> Init() {
            var typ = Type.GetTypeFromCLSID(CLSID, throwOnError: false);
            var obj = typ == null ? null : await ThreadPool.Work(nameof(Activator.CreateInstance), () => Activator.CreateInstance(typ), CancellationToken.None);
            var inst = Identifier = obj as IShellIconOverlayIdentifier;
            if (inst == null) {
                if (obj != null) {
                    Marshal.FinalReleaseComObject(obj);
                }
                throw new InvalidCastException();
            }
            return inst;
        }

        public async Task<int> GetPriority(CancellationToken cancellationToken) {
            var id = Identifier ?? await Init();
            var pr = default(int);
            var hr = await ThreadPool.Work(nameof(id.GetPriority), () => id.GetPriority(out pr), cancellationToken);
            hr.ThrowOnError();
            return pr;
        }

        public async Task<bool> IsMemberOf(string path, CancellationToken cancellationToken) {
            var id = Identifier ?? await Init();
            var hr = await ThreadPool.Work(nameof(id.IsMemberOf), () => id.IsMemberOf(path, 0), cancellationToken);
            switch (hr) {
                case HRESULT.S_OK:
                    return true;
                case HRESULT.S_FALSE:
                    return false;
                default:
                    hr.ThrowOnError();
                    return false;
            }
        }

        public async Task<object> GetOverlaySource(CancellationToken cancellationToken) {
            var id = Identifier ?? await Init();
            var index = default(int);
            var flags = default(ISIOI);
            var sb = new StringBuilder(capacity: 255, maxCapacity: 255);
            var hr = await ThreadPool.Work(nameof(id.GetOverlayInfo), () => id.GetOverlayInfo(sb, 255, out index, out flags), cancellationToken);
            hr.ThrowOnError();

            var fileName = sb.ToString();
            var hIcon = shell32.ExtractIconW(IntPtr.Zero, fileName, index);
            var iIcon = hIcon.ToInt32();
            if (iIcon > 1) {
                try {
                    var source = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    if (source.CanFreeze) {
                        source.Freeze();
                    }
                    return source;
                }
                finally {
                    var success = user32.DestroyIcon(hIcon);
                    if (success == false) {
                        if (Log.Error()) {
                            Log.Error(nameof(user32.DestroyIcon));
                        }
                    }
                }
            }
            return null;
        }
    }
}
