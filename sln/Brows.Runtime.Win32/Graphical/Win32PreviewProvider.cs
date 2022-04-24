using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Gui {
    using Runtime.InteropServices;
    using Runtime.Win32;
    using Threading;

    internal class Win32PreviewProvider : PreviewProvider {
        public StaThreadPool ThreadPool { get; }

        public Win32PreviewProvider(StaThreadPool threadPool) {
            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        }

        public override async Task<object> GetImageSource(IPreviewInput input, ImageSize size, CancellationToken cancellationToken) {
            if (null == input) throw new ArgumentNullException(nameof(input));
            using (var wrapper = new ShellItemImageFactoryWrapper(input.ID, ThreadPool)) {
                var sz = new SIZE { cx = size.Width, cy = size.Height };
                var flags = SIIGBF.BIGGERSIZEOK;
                return await wrapper.GetBitmapSource(sz, flags, cancellationToken);
            }
        }

        //public void Draw(string id, EntryPreviewTarget target) {
        //    const string IPreviewHandler = "{8895b1c6-b41f-4c1c-a562-0d564250836f}";

        //    try {
        //        var buffer = new StringBuilder(1024);
        //        var bufferSz = (uint)buffer.Capacity;
        //        var extension = PATH.GetExtension(id);
        //        var hr = shlwapi.AssocQueryStringW(
        //            ASSOCF.INIT_DEFAULTTOSTAR | ASSOCF.NOTRUNCATE,
        //            ASSOCSTR.SHELLEXTENSION,
        //            extension,
        //            IPreviewHandler,
        //            buffer,
        //            ref bufferSz);
        //        hr.ThrowOnError();

        //        var guid = new Guid(buffer.ToString());
        //        var type = Type.GetTypeFromCLSID(guid);
        //        var inst = Activator.CreateInstance(type);
        //        if (inst is IInitializeWithStream initializer) {
        //            if (inst is IPreviewHandler previewHandler) {

        //                using (var stream = File.OpenRead(id)) {
        //                    var streamWrapper = new StreamWrapper(stream);

        //                    hr = initializer.Initialize(streamWrapper, STGM.READ);
        //                    hr.ThrowOnError();

        //                    var window = (Window)target.Window;
        //                    var helper = new WindowInteropHelper(window);
        //                    var hwnd = helper.Handle;

        //                    //var rectangle = (Rectangle)target.Rectangle;
        //                    var prc = new RECT {
        //                        left = 100,
        //                        top = 0,
        //                        bottom = 100,
        //                        right = 0
        //                    };

        //                    hr = previewHandler.SetWindow(hwnd, ref prc);
        //                    hr.ThrowOnError();

        //                    hr = previewHandler.DoPreview();
        //                    hr.ThrowOnError();

        //                    //hr = previewHandler.Unload();
        //                    hr.ThrowOnError();
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex) {
        //        throw;
        //    }
        //}
    }
}
