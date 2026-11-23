using Domore.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Native;

/// <summary>
/// Loads and releases a native library and reports loading failures.
/// </summary>
public abstract class NativeLoader {
    private static readonly ILog Log = Logging.For(typeof(NativeLoader));
    private static readonly NativeLoader Brows = new BrowsNative();

    private volatile Task HandleTask;
    private readonly Lock HandleLocker = new();

    /// <summary>
    /// Initializes a loader for a native library.
    /// </summary>
    /// <param name="dll">
    /// The native library file name.
    /// </param>
    protected NativeLoader(string dll) {
        Dll = dll;
    }

    /// <summary>
    /// Performs library-specific setup after the library is loaded.
    /// </summary>
    protected virtual void HandleLoaded() {
    }

    /// <summary>
    /// Performs library-specific cleanup before the library is released.
    /// </summary>
    protected virtual void HandleFreeing() {
    }

    /// <summary>
    /// Runs a native operation and throws when its error code is nonzero.
    /// </summary>
    /// <param name="result">
    /// The operation, returning zero on success or an error code on failure.
    /// </param>
    protected void Try(Func<int> result) {
        ArgumentNullException.ThrowIfNull(result);
        var error = result();
        if (error != 0) {
            throw new NativeErrorException(error);
        }
    }

    /// <summary>
    /// Gets the name of the native library to load.
    /// </summary>
    public string Dll { get; }

    /// <summary>
    /// Gets the loaded library's handle, or zero when it is not loaded.
    /// </summary>
    public IntPtr Handle { get; private set; }

    /// <summary>
    /// Gets the most recently recorded loading or release error, or null when none is recorded.
    /// </summary>
    public Exception Error { get; private set; }

    /// <summary>
    /// Ensures that the native library is loaded.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the library is loaded; otherwise, false. Loading failures are available through Error.
    /// </returns>
    public async Task<bool> Loaded(CancellationToken token) {
        if (token.IsCancellationRequested){
            token.ThrowIfCancellationRequested();
        }
        if (this != Brows) {
            await Brows.Loaded(token);
        }
        token.ThrowIfCancellationRequested();
        var task = HandleTask;
        if (task is null) {
            lock (HandleLocker) {
                if (HandleTask is null) {
                    HandleTask = Task.Run(cancellationToken: default, action: () => {
                        try {
                            var handle = NativePath.Load(Dll);
                            try {
                                HandleLoaded();
                            }
                            catch {
                                NativePath.Free(handle);
                                throw;
                            }
                            lock (HandleLocker) {
                                Handle = handle;
                                Error = null;
                            }
                        }
                        catch (Exception ex) {
                            if (Log.Error()) {
                                Log.Error(Log.Join(nameof(Loaded), Dll), ex);
                            }
                            throw;
                        }
                    });
                }
                task = HandleTask;
            }
        }
        if (task.IsCompleted == false || Handle == IntPtr.Zero) {
            try {
                await task.WaitAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                throw;
            }
            catch (Exception ex) {
                lock (HandleLocker) {
                    if (HandleTask == task) {
                        HandleTask = null;
                        Error = ex;
                    }
                }
            }
        }
        return Handle != IntPtr.Zero;
    }

    /// <summary>
    /// Waits for any pending load and releases the native library.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the library has been released.
    /// </returns>
    public async Task Freed(CancellationToken token) {
        // TODO: Call this method somewhere and make sure it works.
        token.ThrowIfCancellationRequested();
        var task = HandleTask;
        if (task != null) {
            /*
             * Wait for an in-flight load to finish before freeing, so the load
             * cannot publish a handle for a library that is already free.
             */
            try {
                await task.WaitAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                throw;
            }
            catch (Exception e) {
                lock (HandleLocker) {
                    if (HandleTask == task) {
                        HandleTask = null;
                        Error = e;
                    }
                }
            }
        }
        token.ThrowIfCancellationRequested();
        lock (HandleLocker) {
            var handle = Handle;
            if (handle != IntPtr.Zero) {
                HandleFreeing();
                NativePath.Free(handle);
                Handle = IntPtr.Zero;
            }
            if (task != null && HandleTask == task) {
                HandleTask = Task.CompletedTask;
                Error = null;
            }
        }
    }
}
