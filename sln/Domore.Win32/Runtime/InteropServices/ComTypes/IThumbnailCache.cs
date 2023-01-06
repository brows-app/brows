using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IThumbnailCache)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IThumbnailCache {
        void GetThumbnail(
            [In] IShellItem pShellItem,
            [In] uint cxyRequestedThumbSize,
            [In] WTS flags /*default:  WTS_FLAGS.WTS_EXTRACT*/,
            [Out][MarshalAs(UnmanagedType.Interface)] out ISharedBitmap ppvThumb,
            [Out] out WTS_CACHEFLAGS pOutFlags,
            [Out] out WTS_THUMBNAILID pThumbnailID
        );

        void GetThumbnailByID(
            [In, MarshalAs(UnmanagedType.Struct)] WTS_THUMBNAILID thumbnailID,
            [In] uint cxyRequestedThumbSize,
            [Out][MarshalAs(UnmanagedType.Interface)] out ISharedBitmap ppvThumb,
            [Out] out WTS_CACHEFLAGS pOutFlags
        );
    }
}
