using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IOperationsProgressDialog)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOperationsProgressDialog {
        [PreserveSig] HRESULT StartProgressDialog([In] IntPtr hwndOwner, [In] uint flags);
        [PreserveSig] HRESULT StopProgressDialog();
        [PreserveSig] HRESULT SetOperation([In] SPACTION action);
        [PreserveSig] HRESULT SetMode([In] PDM mode);
        [PreserveSig]
        HRESULT UpdateProgress(
            [In] ulong ullPointsCurrent,
            [In] ulong ullPointsTotal,
            [In] ulong ullSizeCurrent,
            [In] ulong ullSizeTotal,
            [In] ulong ullItemsCurrent,
            [In] ulong ullItemsTotal);
        [PreserveSig] HRESULT UpdateLocations([In] IShellItem psiSource, [In] IShellItem psiTarget, [In] IShellItem psiItem);
        [PreserveSig] HRESULT ResetTimer();
        [PreserveSig] HRESULT PauseTimer();
        [PreserveSig] HRESULT ResumeTimer();
        [PreserveSig] HRESULT GetMilliseconds([Out] out ulong pullElapsed, [Out] out ulong pullRemaining);
        [PreserveSig] HRESULT GetOperationStatus([Out] out PDOPSTATUS popstatus);
    }
}
