using System;

namespace Domore.Runtime.InteropServices {
    using ComTypes;

    public sealed class FileOperationWrapper : ComObjectWrapper<IFileOperation> {
        protected sealed override IFileOperation Factory() {
            return (IFileOperation)Activator.CreateInstance(CLSID.Managed.Type.FileOperation);
        }

        public IFileOperation FileOperation =>
            ComObject();
    }
}
