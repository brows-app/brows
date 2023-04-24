using System;
using System.IO;

namespace Domore.IO {
    public delegate void DirectoryInfoEnumeratingEventHandler(object sender, DirectoryInfoEnumeratingEventArgs e);

    public sealed class DirectoryInfoEnumeratingEventArgs : EventArgs {
        public bool Ignore { get; set; }

        public FileSystemInfo FileSystemInfo { get; }

        public DirectoryInfoEnumeratingEventArgs(FileSystemInfo fileSystemInfo) {
            FileSystemInfo = fileSystemInfo;
        }
    }
}
