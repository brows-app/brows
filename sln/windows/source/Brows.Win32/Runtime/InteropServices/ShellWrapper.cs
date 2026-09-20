using System;

namespace Brows.Runtime.InteropServices; 
using ComTypes;

public sealed class ShellWrapper : ComObjectWrapper<IShell> {
    protected sealed override IShell Factory() {
        return (IShell)Activator.CreateInstance(CLSID.Managed.Type.Shell);
    }

    public IShell Shell =>
        ComObject();
}
