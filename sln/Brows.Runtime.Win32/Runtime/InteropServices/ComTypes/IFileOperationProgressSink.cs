using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IFileOperationProgressSink)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFileOperationProgressSink {
        [PreserveSig] HRESULT StartOperations();
        [PreserveSig] HRESULT FinishOperations(uint hrResult);
        [PreserveSig] HRESULT PreRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        [PreserveSig] HRESULT PostRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, uint hrRename, IShellItem psiNewlyCreated);
        [PreserveSig] HRESULT PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        [PreserveSig] HRESULT PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, uint hrMove, IShellItem psiNewlyCreated);
        [PreserveSig] HRESULT PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        [PreserveSig] HRESULT PostCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, uint hrCopy, IShellItem psiNewlyCreated);
        [PreserveSig] HRESULT PreDeleteItem(uint dwFlags, IShellItem psiItem);
        [PreserveSig] HRESULT PostDeleteItem(uint dwFlags, IShellItem psiItem, uint hrDelete, IShellItem psiNewlyCreated);
        [PreserveSig] HRESULT PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        [PreserveSig] HRESULT PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName, uint dwFileAttributes, uint hrNew, IShellItem psiNewItem);
        [PreserveSig] HRESULT UpdateProgress(uint iWorkTotal, uint iWorkSoFar);
        [PreserveSig] HRESULT ResetTimer();
        [PreserveSig] HRESULT PauseTimer();
        [PreserveSig] HRESULT ResumeTimer();
    }
}
