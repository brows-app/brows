using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.InteropServices {
    using Logger;

    internal abstract class ComObjectWrapper<T> : IDisposable where T : class {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(ComObjectWrapper<T>)));
        private ILog _Log;

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
                        if (Log.Warn()) {
                            Log.Warn(
                                nameof(Dispose),
                                $"{nameof(T)} > {typeof(T)}",
                                $"{nameof(referenceCount)} > {referenceCount}");
                        }
                    }
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
