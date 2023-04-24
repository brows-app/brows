using Domore.Runtime.InteropServices;
using Domore.Runtime.InteropServices.Extensions;
using Domore.Runtime.Win32;
using Domore.Threading;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public static class Win32Thumbnail {
        private static STAThreadPool ThreadPool =>
            Win32ThreadPool.Common;

        public static async Task<object> GetImageSource(string path, int width, int height, CancellationToken cancellationToken) {
            return await ThreadPool.Work(nameof(Win32Thumbnail), cancellationToken: cancellationToken, work: () => {
                using (var wrapper = new ShellItemImageFactoryWrapper(path)) {
                    var sz = new SIZE { cx = width, cy = height };
                    var flags = SIIGBF.THUMBNAILONLY | SIIGBF.BIGGERSIZEOK | SIIGBF.INCACHEONLY;
                    try {
                        return wrapper.GetBitmapSource(sz, flags);
                    }
                    catch {
                    }
                    flags = SIIGBF.THUMBNAILONLY;
                    try {
                        return wrapper.GetBitmapSource(sz, flags);
                    }
                    catch {
                    }
                    flags = SIIGBF.RESIZETOFIT;
                    return wrapper.GetBitmapSource(sz, flags);
                }
            });
        }
    }
}
