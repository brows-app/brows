using System.IO;

namespace Domore.IO {
    public delegate void FileSystemNotifyEventHandler(object sender, FileSystemNotifyEventArgs e);

    public sealed class FileSystemNotifyEventArgs {
        public bool Break { get; set; }

        public RenamedEventArgs RenamedEvent { get; }
        public FileSystemEventArgs FileSystemEvent { get; }

        public FileSystemNotifyEventArgs(FileSystemEventArgs fileSystemEvent) {
            FileSystemEvent = fileSystemEvent;
            RenamedEvent = FileSystemEvent as RenamedEventArgs;
        }
    }
}
