using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Domore.Runtime.InteropServices.Extensions {
    using Logs;
    using Win32;

    public static class ShellIconOverlayIdentifierWrapperExtension {
        private static readonly ILog Log = Logging.For(typeof(ShellIconOverlayIdentifierWrapperExtension));

        public static BitmapSource GetOverlaySource(this ShellIconOverlayIdentifierWrapper shellIconOverlayIdentifierWrapper) {
            if (null == shellIconOverlayIdentifierWrapper) throw new ArgumentNullException(nameof(shellIconOverlayIdentifierWrapper));
            var file = shellIconOverlayIdentifierWrapper.GetOverlayInfo(out var index, out var flags);
            var hIcon = shell32.ExtractIconW(IntPtr.Zero, file, (uint)index);
            var iIcon = hIcon.ToInt64();
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
            return null;
        }
    }
}
