using System;
using System.IO;

namespace Brows {
    internal sealed class FoundInFileError : FoundInInfo {
        public Exception Exception { get; }

        public FoundInFileError(FileInfo file, DirectoryInfo root, Exception exception) : base(file, root) {
            Exception = exception;
        }
    }
}
