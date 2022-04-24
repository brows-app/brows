using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Brows.Runtime.InteropServices {
    using ComTypes;
    using Threading;
    using Win32;


    internal sealed class ShellItemImageFactoryWrapper : ComObjectWrapper<IShellItemImageFactory> {
        private static readonly Guid IID_IShellItem = new Guid(IID.IShellItem);

        protected sealed override IShellItemImageFactory Factory() {
            var
            hr = shell32.SHCreateItemFromParsingName(Name, IntPtr.Zero, IID_IShellItem, out var ppv);
            hr.ThrowOnError();

            var inst = ppv as IShellItemImageFactory;
            if (inst != null) return inst;

            Marshal.FinalReleaseComObject(ppv);
            throw new InvalidCastException();
        }

        public string Name { get; }
        public StaThreadPool ThreadPool { get; }

        public ShellItemImageFactoryWrapper(string name, StaThreadPool threadPool) {
            Name = name;
            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        }

        public async Task<BitmapSource> GetBitmapSource(SIZE size, SIIGBF flags, CancellationToken cancellationToken) {
            var bm = default(IntPtr);
            var co = await ThreadPool.Work(ComObject, cancellationToken);
            try {
                var
                hr = await ThreadPool.Work(() => co.GetImage(size, flags, out bm), cancellationToken);
                hr.ThrowOnError();

                var source = Imaging.CreateBitmapSourceFromHBitmap(bm, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                if (source.CanFreeze) {
                    source.Freeze();
                }
                return source;
            }
            finally {
                if (bm != default(IntPtr)) {
                    gdi32.DeleteObject(bm);
                }
            }
        }
    }
}
