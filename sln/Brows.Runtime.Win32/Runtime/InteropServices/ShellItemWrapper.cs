using System;

namespace Brows.Runtime.InteropServices {
    using ComTypes;
    using Win32;

    internal sealed class ShellItemWrapper : ComObjectWrapper<IShellItem> {
        protected sealed override IShellItem Factory() {
            var
            hr = shell32.SHCreateItemFromParsingName(Path, IntPtr.Zero, typeof(IShellItem).GUID, out var shellItem);
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
