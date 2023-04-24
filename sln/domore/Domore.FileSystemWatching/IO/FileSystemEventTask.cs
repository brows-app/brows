using System;

namespace Domore.IO {
    public abstract class FileSystemEventTask : IDisposable {
        protected virtual void Dispose(bool disposing) {
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~FileSystemEventTask() {
            Dispose(false);
        }
    }
}
