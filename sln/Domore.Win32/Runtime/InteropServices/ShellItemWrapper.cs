using System;

namespace Domore.Runtime.InteropServices {
    using ComTypes;
    using Win32;

    public sealed class ShellItemWrapper : ComObjectWrapper<IShellItem> {
        protected sealed override IShellItem Factory() {
            var iid = IID.Managed.IShellItem;
            var
            hr = shell32.SHCreateItemFromParsingName(Path, IntPtr.Zero, ref iid, out var shellItem);
            hr.ThrowOnError();
            return shellItem;
        }

        public IShellItem ShellItem =>
            ComObject();

        public string Path { get; }

        public ShellItemWrapper(string path) {
            Path = path;
        }
    }
}
