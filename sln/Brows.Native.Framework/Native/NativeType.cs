using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;

namespace Brows.Native {
    public abstract class NativeType : CriticalFinalizerObject, IDisposable {
        private IntPtr Handle {
            get {
                if (_Handle == IntPtr.Zero) {
                    var handle = _Handle = Create();
                    if (handle == IntPtr.Zero) {
                        throw new OutOfMemoryException();
                    }
                    Init(handle);
                }
                return _Handle;
            }
        }
        private IntPtr _Handle;

        protected abstract IntPtr Create();
        protected abstract void Destroy(IntPtr handle);

        protected virtual void Init(IntPtr handle) {
        }

        protected bool GetBoolean(Func<IntPtr, int> get) {
            if (null == get) throw new ArgumentNullException(nameof(get));
            return get(Handle) != 0;
        }

        protected void SetBoolean(Func<IntPtr, int, int> set, bool value) {
            if (null == set) throw new ArgumentNullException(nameof(set));
            Try(() => set(Handle, value ? 1 : 0));
        }

        protected long GetInt64(Func<IntPtr, long> get) {
            if (null == get) throw new ArgumentNullException(nameof(get));
            return get(Handle);
        }

        protected void SetInt64(Func<IntPtr, long, int> set, long value) {
            if (null == set) throw new ArgumentNullException(nameof(set));
            Try(() => set(Handle, value));
        }

        protected int GetInt32(Func<IntPtr, int> get) {
            if (null == get) throw new ArgumentNullException(nameof(get));
            return get(Handle);
        }

        protected void SetInt32(Func<IntPtr, int, int> set, int value) {
            if (null == set) throw new ArgumentNullException(nameof(set));
            Try(() => set(Handle, value));
        }

        protected IntPtr GetIntPtr(Func<IntPtr, IntPtr> get) {
            if (null == get) throw new ArgumentNullException(nameof(get));
            return get(Handle);
        }

        protected void SetIntPtr(Func<IntPtr, IntPtr, int> set, IntPtr value) {
            if (null == set) throw new ArgumentNullException(nameof(set));
            Try(() => set(Handle, value));
        }

        protected string GetAnsiString(Func<IntPtr, IntPtr> get) {
            if (null == get) throw new ArgumentNullException(nameof(get));
            var p = get(Handle);
            return Marshal.PtrToStringAnsi(p);
        }

        protected void SetAnsiString(Func<IntPtr, IntPtr, int> set, string value) {
            if (null == set) throw new ArgumentNullException(nameof(set));
            var p = Marshal.StringToHGlobalAnsi(value);
            try {
                Try(() => set(Handle, p));
            }
            finally {
                Marshal.FreeHGlobal(p);
            }
        }

        protected SecureString GetSecureString(Func<IntPtr, IntPtr> get) {
            if (null == get) throw new ArgumentNullException(nameof(get));
            return new SecureString();
        }

        protected void SetSecureString(Func<IntPtr, IntPtr, int> set, SecureString value) {
            if (null == set) throw new ArgumentNullException(nameof(set));
            var p = value == null ? IntPtr.Zero : Marshal.SecureStringToGlobalAllocAnsi(value);
            try {
                Try(() => set(Handle, p));
            }
            finally {
                Marshal.FreeHGlobal(p);
            }
        }

        protected void Try(Func<int> result, Func<int, Exception> exceptionFactory = null) {
            if (null == result) throw new ArgumentNullException(nameof(result));
            var error = result();
            if (error != 0) {
                throw exceptionFactory?.Invoke(error) ?? new NativeErrorException(error);
            }
        }

        protected void Try(Func<IntPtr, int> action, Func<int, Exception> exceptionFactory = null) {
            if (null == action) throw new ArgumentNullException(nameof(action));
            Try(() => action(Handle), exceptionFactory);
        }

        protected virtual void Dispose(bool disposing) {
            var handle = _Handle;
            if (handle != IntPtr.Zero) {
                Destroy(handle);
            }
            _Handle = IntPtr.Zero;
        }

        public event EventHandler Disposed;

        public IntPtr GetHandle() {
            return Handle == IntPtr.Zero
                ? throw new InvalidOperationException()
                : Handle;
        }

        public void Dispose() {
            Dispose(true);
            Disposed?.Invoke(this, EventArgs.Empty);
            GC.SuppressFinalize(this);
        }

        ~NativeType() {
            Dispose(false);
        }
    }
}
