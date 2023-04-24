using System;

namespace Brows {
    public class EntryStreamReady : IEntryStreamReady {
        protected virtual void Dispose(bool disposing) {
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~EntryStreamReady() {
            Dispose(false);
        }
    }
}
