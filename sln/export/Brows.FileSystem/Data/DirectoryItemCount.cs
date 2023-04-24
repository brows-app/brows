using System;
using System.IO;

namespace Brows.Data {
    internal class DirectoryItemCount : DirectoryAggregateData {
        protected sealed override long Data(FileSystemInfo info) {
            switch (Kinds) {
                case DirectoryItemCountKinds.Any:
                case DirectoryItemCountKinds.Directory | DirectoryItemCountKinds.File:
                    return 1;
                case DirectoryItemCountKinds.Directory:
                    return info is DirectoryInfo ? 1 : 0;
                case DirectoryItemCountKinds.File:
                    return info is FileInfo ? 1 : 0;
                default:
                    throw new InvalidOperationException();
            }
        }

        public DirectoryItemCountKinds Kinds { get; }

        public DirectoryItemCount(DirectoryItemCountKinds kinds) {
            Kinds = kinds;
            Width = 100;
        }

        public DirectoryItemCount() : this(DirectoryItemCountKinds.Any) {
        }
    }
}
