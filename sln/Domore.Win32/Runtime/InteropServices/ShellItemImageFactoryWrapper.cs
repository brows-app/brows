using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices {
    using ComTypes;
    using Win32;

    public sealed class ShellItemImageFactoryWrapper : ComObjectWrapper<IShellItemImageFactory> {
        protected sealed override IShellItemImageFactory Factory() {
            var iid = IID.Managed.IShellItem;
            var
            hr = shell32.SHCreateItemFromParsingName(Path, IntPtr.Zero, ref iid, out var ppv);
            hr.ThrowOnError();

            var inst = ppv as IShellItemImageFactory;
            if (inst == null) {
                Marshal.FinalReleaseComObject(ppv);
                throw new InvalidCastException();
            }
            return inst;
        }

        public string Path { get; }

        public ShellItemImageFactoryWrapper(string path) {
            Path = path;
        }

        public IntPtr GetImage(SIZE size, SIIGBF flags) {
            var co = ComObject();
            var hr = co.GetImage(size, flags, out var bm);
            hr.ThrowOnError();
            return bm;
        }
    }
}
