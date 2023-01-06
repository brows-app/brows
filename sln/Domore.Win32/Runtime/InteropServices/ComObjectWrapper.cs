using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices {
    public abstract class ComObjectWrapper<T> : CriticalFinalizerObject, IDisposable where T : class {
        protected abstract T Factory();

        protected T ComObject() =>
            _ComObject ?? (
            _ComObject = Factory());
        private T _ComObject;

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                if (_ComObject != null) {
                    var referenceCount = Marshal.FinalReleaseComObject(_ComObject);
                    if (referenceCount == 0) {
                        _ComObject = null;
                    }
                    else {
                        // TODO: Log this.
                    }
                }
            }
            else {
                if (_ComObject != null) {
                    Marshal.FinalReleaseComObject(_ComObject);
                }
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ComObjectWrapper() {
            Dispose(false);
        }
    }
}
