using Domore.Runtime;
using System;

namespace Domore.IO {
    internal sealed class FileSystemPath {
        public bool IsCaseSensitive(string path) {
            return
                OperatingSystem.IsWindows() ? Win32Path.IsCaseSensitive(path) :
                throw new NotImplementedException();
        }
    }
}
