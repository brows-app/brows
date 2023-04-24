//using Domore.Logs;
//using Domore.Runtime.InteropServices;
//using Domore.Runtime.InteropServices.Extensions;
//using Domore.Runtime.Win32;
//using Domore.Threading;
//using System;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Brows.Gui {
//    internal class Win32PreviewProvider : PreviewProvider {
//        private static readonly ILog Log = Logging.For(typeof(Win32PreviewProvider));

//        private Task PreviewTask = Task.CompletedTask;

//        private async Task<T> PreviewWait<T>(Task<T> task) {
//            if (null == task) throw new ArgumentNullException(nameof(task));
//            try {
//                await PreviewTask;
//            }
//            catch {
//            }
//            return await (Task<T>)(PreviewTask = task);
//        }

//        public STAThreadPool ThreadPool { get; }

//        public Win32PreviewProvider(STAThreadPool threadPool) {
//            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
//        }

//        public override async Task<object> GetImageSource(IPreviewInput input, ImageSize size, CancellationToken cancellationToken) {
//            if (null == input) throw new ArgumentNullException(nameof(input));
//            return await ThreadPool.Work(nameof(Win32PreviewProvider), cancellationToken: cancellationToken, work: () => {
//                using (var wrapper = new ShellItemImageFactoryWrapper(input.ID)) {
//                    var sz = new SIZE { cx = size.Width, cy = size.Height };
//                    var flags = SIIGBF.BIGGERSIZEOK;
//                    return wrapper.GetBitmapSource(sz, flags);
//                }
//            });
//        }
//    }
//}
