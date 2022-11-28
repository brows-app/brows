﻿using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Brows.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IShellIconOverlayIdentifier)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellIconOverlayIdentifier {
        [PreserveSig]
        HRESULT IsMemberOf([MarshalAs(UnmanagedType.LPWStr)] string pwszPath, uint dwAttrib);

        [PreserveSig]
        HRESULT GetOverlayInfo(StringBuilder pwszIconFile, int cchMax, out int pIndex, out ISIOI pdwFlags);

        [PreserveSig]
        HRESULT GetPriority(out int pPriority);
    }
}
