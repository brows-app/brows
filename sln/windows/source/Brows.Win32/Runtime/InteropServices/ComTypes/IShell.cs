using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.InteropServices.ComTypes; 
[Guid(IID.IShell)]
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShell {
}
