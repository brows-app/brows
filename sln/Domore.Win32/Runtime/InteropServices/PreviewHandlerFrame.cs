//using System;
//using System.Runtime.InteropServices;

//namespace Domore.Runtime.InteropServices {
//    using ComTypes;
//    using Win32;

//    [Guid("6A0AC422-4D39-49C3-B3A7-3CD79C6016B6")]
//    [ComVisible(true)]
//    [ClassInterface(ClassInterfaceType.None)]
//    public class PreviewHandlerFrame : IPreviewHandlerFrame {
//        HRESULT IPreviewHandlerFrame.GetWindowContext(out PREVIEWHANDLERFRAMEINFO pinfo) {
//            pinfo.haccel = user32.CreateAcceleratorTableW(new ACCEL[] { }, 0);
//            pinfo.cAccelEntries = 0;
//            return HRESULT.S_OK;
//        }

//        HRESULT IPreviewHandlerFrame.TranslateAccelerator(ref MSG pmsg) {
//            return HRESULT.S_OK;
//        }
//    }
//}
