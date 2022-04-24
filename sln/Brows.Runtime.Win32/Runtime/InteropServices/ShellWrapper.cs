using System;

namespace Brows.Runtime.InteropServices {
    using ComTypes;

    internal sealed class ShellWrapper : ComObjectWrapper<IShell> {
        private static readonly Guid CLSIDShell = new Guid(CLSID.Shell);
        private static readonly Type ShellType = Type.GetTypeFromCLSID(CLSIDShell);

        protected sealed override IShell Factory() {
            return (IShell)Activator.CreateInstance(ShellType);
        }

        public IShell Shell =>
            ComObject();
    }
}
