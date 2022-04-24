using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Gui {
    using Runtime.InteropServices;
    using Runtime.Win32;
    using Threading;

    internal class Win32ThumbnailProvider : ThumbnailProvider {
        public StaThreadPool ThreadPool { get; }

        public Win32ThumbnailProvider(StaThreadPool threadPool) {
            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        }

        public override async Task<object> GetImageSource(IThumbnailInput input, ImageSize size, CancellationToken cancellationToken) {
            if (null == input) throw new ArgumentNullException(nameof(input));

            using (var wrapper = new ShellItemImageFactoryWrapper(input.ID, ThreadPool)) {
                var sz = new SIZE { cx = size.Width, cy = size.Height };
                var flags = SIIGBF.THUMBNAILONLY | SIIGBF.BIGGERSIZEOK | SIIGBF.INCACHEONLY;
                try {
                    return await wrapper.GetBitmapSource(sz, flags, cancellationToken);
                }
                catch {
                }
                flags = SIIGBF.THUMBNAILONLY;
                try {
                    return await wrapper.GetBitmapSource(sz, flags, cancellationToken);
                }
                catch {
                }
                flags = SIIGBF.RESIZETOFIT;
                return await wrapper.GetBitmapSource(sz, flags, cancellationToken);
            }
        }
    }
}
