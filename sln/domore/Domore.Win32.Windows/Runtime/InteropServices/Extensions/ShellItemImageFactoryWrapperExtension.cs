using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Domore.Runtime.InteropServices.Extensions {
    using Logs;
    using Win32;

    public static class ShellItemImageFactoryWrapperExtension {
        private static readonly ILog Log = Logging.For(typeof(ShellItemImageFactoryWrapperExtension));

        public static BitmapSource GetBitmapSource(this ShellItemImageFactoryWrapper shellItemImageFactoryWrapper, SIZE size, SIIGBF flags) {
            if (null == shellItemImageFactoryWrapper) throw new ArgumentNullException(nameof(shellItemImageFactoryWrapper));
            var bm = default(IntPtr);
            try {
                bm = shellItemImageFactoryWrapper.GetImage(size, flags);
                var source = Imaging.CreateBitmapSourceFromHBitmap(bm, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                if (source.CanFreeze) {
                    source.Freeze();
                }
                return source;
            }
            finally {
                if (bm != default(IntPtr)) {
                    var success = gdi32.DeleteObject(bm);
                    if (success == false) {
                        try {
                            throw new Win32Exception();
                        }
                        catch (Exception ex) {
                            if (Log.Warn()) {
                                Log.Warn(ex);
                            }
                        }
                    }
                }
            }
        }
    }
}
