using System;

namespace Brows.Runtime.InteropServices {
    using ComTypes;

    internal sealed class FileOperationWrapper : ComObjectWrapper<IFileOperation> {
        private static readonly Guid CLSIDFileOperation = new Guid(CLSID.FileOperation);
        private static readonly Type FileOperationType = Type.GetTypeFromCLSID(CLSIDFileOperation);

        protected sealed override IFileOperation Factory() {
            return (IFileOperation)Activator.CreateInstance(FileOperationType);
        }

        public IFileOperation FileOperation =>
            ComObject();
    }
}
