using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;

namespace Brows.Native;

public abstract class NativeType : CriticalFinalizerObject, IDisposable {
    private readonly Lock Locker = new();
    private IntPtr _Handle;

    private IntPtr Handle {
        get {
            lock (Locker) {
                var handle = _Handle;
                if (handle == IntPtr.Zero) {
                    handle = Create();
                    if (handle == IntPtr.Zero) {
                        throw new OutOfMemoryException();
                    }
                    try {
                        Init(handle);
                    }
                    catch {
                        Destroy(handle);
                        throw;
                    }
                    _Handle = handle;
                }
                return handle;
            }
        }
    }

    protected abstract IntPtr Create();
    protected abstract void Destroy(IntPtr handle);

    protected virtual void Init(IntPtr handle) {
    }

    protected bool GetBoolean(Func<IntPtr, int> get) {
        ArgumentNullException.ThrowIfNull(get);
        return get(Handle) != 0;
    }

    protected void SetBoolean(Func<IntPtr, int, int> set, bool value) {
        ArgumentNullException.ThrowIfNull(set);
        Try(() => set(Handle, value ? 1 : 0));
    }

    protected long GetInt64(Func<IntPtr, long> get) {
        ArgumentNullException.ThrowIfNull(get);
        return get(Handle);
    }

    protected void SetInt64(Func<IntPtr, long, int> set, long value) {
        ArgumentNullException.ThrowIfNull(set);
        Try(() => set(Handle, value));
    }

    protected int GetInt32(Func<IntPtr, int> get) {
        ArgumentNullException.ThrowIfNull(get);
        return get(Handle);
    }

    protected void SetInt32(Func<IntPtr, int, int> set, int value) {
        ArgumentNullException.ThrowIfNull(set);
        Try(() => set(Handle, value));
    }

    protected IntPtr GetIntPtr(Func<IntPtr, IntPtr> get) {
        ArgumentNullException.ThrowIfNull(get);
        return get(Handle);
    }

    protected void SetIntPtr(Func<IntPtr, IntPtr, int> set, IntPtr value) {
        ArgumentNullException.ThrowIfNull(set);
        Try(() => set(Handle, value));
    }

    protected string GetAnsiString(Func<IntPtr, IntPtr> get) {
        ArgumentNullException.ThrowIfNull(get);
        var p = get(Handle);
        return Marshal.PtrToStringAnsi(p);
    }

    protected void SetAnsiString(Func<IntPtr, IntPtr, int> set, string value) {
        ArgumentNullException.ThrowIfNull(set);
        var p = Marshal.StringToHGlobalAnsi(value);
        try {
            Try(() => set(Handle, p));
        }
        finally {
            Marshal.FreeHGlobal(p);
        }
    }

    protected void SetSecureString(Func<IntPtr, IntPtr, int> set, SecureString value) {
        ArgumentNullException.ThrowIfNull(set);
        var p = value == null ? IntPtr.Zero : Marshal.SecureStringToGlobalAllocAnsi(value);
        try {
            Try(() => set(Handle, p));
        }
        finally {
            Marshal.FreeHGlobal(p);
        }
    }

    protected void Try(Func<int> result, Func<int, Exception> exceptionFactory = null) {
        ArgumentNullException.ThrowIfNull(result);
        var error = result();
        if (error != 0) {
            throw exceptionFactory?.Invoke(error) ?? new NativeErrorException(error);
        }
    }

    protected void Try(Func<IntPtr, int> action, Func<int, Exception> exceptionFactory = null) {
        ArgumentNullException.ThrowIfNull(action);
        Try(() => action(Handle), exceptionFactory);
    }

    protected virtual void Dispose(bool disposing) {
        lock (Locker) {
            var handle = _Handle;
            if (handle == IntPtr.Zero) {
                return;
            }
            _Handle = IntPtr.Zero;
            Destroy(handle);
        }
    }

    public event EventHandler Disposed;

    public IntPtr GetHandle() {
        var handle = Handle;
        return handle == IntPtr.Zero
            ? throw new InvalidOperationException()
            : handle;
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
