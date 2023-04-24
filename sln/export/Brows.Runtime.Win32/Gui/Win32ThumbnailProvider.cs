//using Domore.Runtime.InteropServices;
//using Domore.Runtime.InteropServices.Extensions;
//using Domore.Runtime.Win32;
//using Domore.Threading;
//using System;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Brows.Gui {
//    internal class Win32ThumbnailProvider : ThumbnailProvider {
//        public STAThreadPool ThreadPool { get; }

//        public Win32ThumbnailProvider(STAThreadPool threadPool) {
//            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
//        }

//        public override async Task<object> GetImageSource(IThumbnailInput input, ImageSize size, CancellationToken cancellationToken) {
//            if (null == input) throw new ArgumentNullException(nameof(input));
//            return await ThreadPool.Work(nameof(Win32ThumbnailProvider), cancellationToken: cancellationToken, work: () => {
//                using (var wrapper = new ShellItemImageFactoryWrapper(input.ID)) {
//                    var sz = new SIZE { cx = size.Width, cy = size.Height };
//                    var flags = SIIGBF.THUMBNAILONLY | SIIGBF.BIGGERSIZEOK | SIIGBF.INCACHEONLY;
//                    try {
//                        return wrapper.GetBitmapSource(sz, flags);
//                    }
//                    catch {
//                    }
//                    flags = SIIGBF.THUMBNAILONLY;
//                    try {
//                        return wrapper.GetBitmapSource(sz, flags);
//                    }
//                    catch {
//                    }
//                    flags = SIIGBF.RESIZETOFIT;
//                    return wrapper.GetBitmapSource(sz, flags);
//                }
//            });
//        }
//    }
//}
