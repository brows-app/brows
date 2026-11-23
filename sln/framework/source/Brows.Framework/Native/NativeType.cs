using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;

namespace Brows.Native;

/// <summary>
/// Owns a native object and provides helpers for reading and writing its values.
/// </summary>
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

    /// <summary>
    /// Creates the native object.
    /// </summary>
    /// <returns>
    /// The object's handle, or zero if creation fails.
    /// </returns>
    protected abstract IntPtr Create();

    /// <summary>
    /// Releases a native object created by <see cref="Create"/>.
    /// </summary>
    /// <param name="handle">
    /// The handle of the object to release.
    /// </param>
    protected abstract void Destroy(IntPtr handle);

    /// <summary>
    /// Initializes a newly created native object before it is used.
    /// </summary>
    /// <param name="handle">
    /// The handle returned by Create.
    /// </param>
    protected virtual void Init(IntPtr handle) {
    }

    /// <summary>
    /// Reads a Boolean value from the native object.
    /// </summary>
    /// <param name="get">
    /// Reads an integer from the supplied native handle, where nonzero means true.
    /// </param>
    /// <returns>
    /// True if the native getter returns a nonzero value; otherwise, false.
    /// </returns>
    protected bool GetBoolean(Func<IntPtr, int> get) {
        ArgumentNullException.ThrowIfNull(get);
        return get(Handle) != 0;
    }

    /// <summary>
    /// Writes a Boolean value to the native object, throwing on a nonzero error code.
    /// </summary>
    /// <param name="set">
    /// Writes the value to the supplied native handle and returns zero on success.
    /// </param>
    /// <param name="value">
    /// The value to write.
    /// </param>
    protected void SetBoolean(Func<IntPtr, int, int> set, bool value) {
        ArgumentNullException.ThrowIfNull(set);
        Try(() => set(Handle, value ? 1 : 0));
    }

    /// <summary>
    /// Reads a 64-bit integer value from the native object.
    /// </summary>
    /// <param name="get">
    /// Reads the value from the supplied native handle.
    /// </param>
    /// <returns>
    /// The value returned by the native getter.
    /// </returns>
    protected long GetInt64(Func<IntPtr, long> get) {
        ArgumentNullException.ThrowIfNull(get);
        return get(Handle);
    }

    /// <summary>
    /// Writes a 64-bit integer value to the native object, throwing on a nonzero error code.
    /// </summary>
    /// <param name="set">
    /// Writes the value to the supplied native handle and returns zero on success.
    /// </param>
    /// <param name="value">
    /// The value to write.
    /// </param>
    protected void SetInt64(Func<IntPtr, long, int> set, long value) {
        ArgumentNullException.ThrowIfNull(set);
        Try(() => set(Handle, value));
    }

    /// <summary>
    /// Reads a 32-bit integer value from the native object.
    /// </summary>
    /// <param name="get">
    /// Reads the value from the supplied native handle.
    /// </param>
    /// <returns>
    /// The value returned by the native getter.
    /// </returns>
    protected int GetInt32(Func<IntPtr, int> get) {
        ArgumentNullException.ThrowIfNull(get);
        return get(Handle);
    }

    /// <summary>
    /// Writes a 32-bit integer value to the native object, throwing on a nonzero error code.
    /// </summary>
    /// <param name="set">
    /// Writes the value to the supplied native handle and returns zero on success.
    /// </param>
    /// <param name="value">
    /// The value to write.
    /// </param>
    protected void SetInt32(Func<IntPtr, int, int> set, int value) {
        ArgumentNullException.ThrowIfNull(set);
        Try(() => set(Handle, value));
    }

    /// <summary>
    /// Reads a pointer value from the native object.
    /// </summary>
    /// <param name="get">
    /// Reads the value from the supplied native handle.
    /// </param>
    /// <returns>
    /// The value returned by the native getter.
    /// </returns>
    protected IntPtr GetIntPtr(Func<IntPtr, IntPtr> get) {
        ArgumentNullException.ThrowIfNull(get);
        return get(Handle);
    }

    /// <summary>
    /// Writes a pointer value to the native object, throwing on a nonzero error code.
    /// </summary>
    /// <param name="set">
    /// Writes the value to the supplied native handle and returns zero on success.
    /// </param>
    /// <param name="value">
    /// The value to write.
    /// </param>
    protected void SetIntPtr(Func<IntPtr, IntPtr, int> set, IntPtr value) {
        ArgumentNullException.ThrowIfNull(set);
        Try(() => set(Handle, value));
    }

    /// <summary>
    /// Reads an ANSI string from the native object.
    /// </summary>
    /// <param name="get">
    /// Returns the address of a null-terminated ANSI string for the supplied handle.
    /// </param>
    /// <returns>
    /// The managed string, or null for a zero address.
    /// </returns>
    protected string GetAnsiString(Func<IntPtr, IntPtr> get) {
        ArgumentNullException.ThrowIfNull(get);
        var p = get(Handle);
        return Marshal.PtrToStringAnsi(p);
    }

    /// <summary>
    /// Passes an ANSI string to a native setter, throwing on a nonzero error code.
    /// </summary>
    /// <param name="set">
    /// Accepts the handle and a string address valid only during the call; returns zero on success.
    /// </param>
    /// <param name="value">
    /// The string to pass.
    /// </param>
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

    /// <summary>
    /// Passes a secure string to a native setter as ANSI text, throwing on a nonzero error code.
    /// </summary>
    /// <param name="set">
    /// Accepts the handle and a string address valid only during the call; returns zero on success.
    /// </param>
    /// <param name="value">
    /// The secure string to pass, or null for a zero address.
    /// </param>
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

    /// <summary>
    /// Runs a native operation and throws for a nonzero error code.
    /// </summary>
    /// <param name="result">
    /// The operation, returning zero on success.
    /// </param>
    /// <param name="exceptionFactory">
    /// Creates an exception from the error code, or null to use NativeErrorException.
    /// </param>
    protected void Try(Func<int> result, Func<int, Exception> exceptionFactory = null) {
        ArgumentNullException.ThrowIfNull(result);
        var error = result();
        if (error != 0) {
            throw exceptionFactory?.Invoke(error) ?? new NativeErrorException(error);
        }
    }

    /// <summary>
    /// Runs an operation on this native object and throws for a nonzero error code.
    /// </summary>
    /// <param name="action">
    /// Accepts the native handle and returns zero on success.
    /// </param>
    /// <param name="exceptionFactory">
    /// Creates an exception from the error code, or null to use NativeErrorException.
    /// </param>
    protected void Try(Func<IntPtr, int> action, Func<int, Exception> exceptionFactory = null) {
        ArgumentNullException.ThrowIfNull(action);
        Try(() => action(Handle), exceptionFactory);
    }

    /// <summary>
    /// Releases the native object if it has been created.
    /// </summary>
    /// <param name="disposing">
    /// True for explicit disposal; false when called during finalization.
    /// </param>
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

    /// <summary>
    /// Occurs after an explicit call to dispose of the native object.
    /// </summary>
    public event EventHandler Disposed;

    /// <summary>
    /// Gets the native object's handle, creating the object if needed.
    /// </summary>
    /// <returns>
    /// The native object's handle.
    /// </returns>
    /// <exception cref="OutOfMemoryException">
    /// The native object could not be created and returned a zero handle.
    /// </exception>
    public IntPtr GetHandle() {
        var handle = Handle;
        return handle == IntPtr.Zero
            ? throw new InvalidOperationException()
            : handle;
    }

    /// <summary>
    /// Releases the native object and raises <see cref="Disposed"/>.
    /// </summary>
    public void Dispose() {
        Dispose(true);
        Disposed?.Invoke(this, EventArgs.Empty);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the native object during finalization.
    /// </summary>
    ~NativeType() {
        Dispose(false);
    }
}
