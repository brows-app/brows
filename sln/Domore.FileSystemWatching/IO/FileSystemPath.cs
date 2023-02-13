using System;

namespace Domore.IO {
    using Runtime;

    internal class FileSystemPath {
        public bool IsCaseSensitive(string path) {
            return
                OperatingSystem.IsWindows() ? Win32Path.IsCaseSensitive(path) :
                throw new NotImplementedException();
        }
    }
}
