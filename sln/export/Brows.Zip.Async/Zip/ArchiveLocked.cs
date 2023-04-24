using System;

namespace Brows.Zip {
    internal abstract class ArchiveLocked : IDisposable {
        protected virtual void Dispose(bool disposing) {
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ArchiveLocked() {
            Dispose(false);
        }
    }
}
