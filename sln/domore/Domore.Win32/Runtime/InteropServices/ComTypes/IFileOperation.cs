using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IFileOperation)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileOperation {
        [PreserveSig] HRESULT Advise(IFileOperationProgressSink pfops, [Out] out uint pdwCookie);
        [PreserveSig] HRESULT Unadvise(uint dwCookie);
        [PreserveSig] HRESULT SetOperationFlags(uint dwOperationFlags);
        [PreserveSig] HRESULT SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
        [PreserveSig] HRESULT SetProgressDialog([MarshalAs(UnmanagedType.Interface)] object popd);
        [PreserveSig] HRESULT SetProperties([MarshalAs(UnmanagedType.Interface)] object pproparray);
        [PreserveSig] HRESULT SetOwnerWindow(IntPtr hwndParent);
        [PreserveSig] HRESULT ApplyPropertiesToItem(IShellItem psiItem);
        [PreserveSig] HRESULT ApplyPropertiesToItems([MarshalAs(UnmanagedType.Interface)] object punkItems);
        [PreserveSig] HRESULT RenameItem(IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IFileOperationProgressSink pfopsItem);
        [PreserveSig] HRESULT RenameItems([MarshalAs(UnmanagedType.Interface)] object pUnkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        [PreserveSig] HRESULT MoveItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IFileOperationProgressSink pfopsItem);
        [PreserveSig] HRESULT MoveItems([MarshalAs(UnmanagedType.Interface)] object punkItems, IShellItem psiDestinationFolder);
        [PreserveSig] HRESULT CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszCopyName, IFileOperationProgressSink pfopsItem);
        [PreserveSig] HRESULT CopyItems([MarshalAs(UnmanagedType.Interface)] object punkItems, IShellItem psiDestinationFolder);
        [PreserveSig] HRESULT DeleteItem(IShellItem psiItem, IFileOperationProgressSink pfopsItem);
        [PreserveSig] HRESULT DeleteItems([MarshalAs(UnmanagedType.Interface)] object punkItems);
        [PreserveSig] HRESULT NewItem(IShellItem psiDestinationFolder, FileAttributes dwFileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string pszName, [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName, IFileOperationProgressSink pfopsItem);
        [PreserveSig] HRESULT PerformOperations();
        [PreserveSig] HRESULT GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)][Out] out bool pfAnyOperationsAborted);
    }
}
