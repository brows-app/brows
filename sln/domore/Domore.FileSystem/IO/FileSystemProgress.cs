using System.IO;

namespace Domore.IO {
    public abstract class FileSystemProgress {
        public abstract void AddToTarget(long value);
        public abstract void AddToProgress(long value);
        public abstract void SetCurrentInfo(FileSystemInfo value);
    }
}
