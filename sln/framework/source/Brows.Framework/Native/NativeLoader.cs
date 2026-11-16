using Domore.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Native;

public abstract class NativeLoader {
    private static readonly ILog Log = Logging.For(typeof(NativeLoader));
    private static readonly NativeLoader Brows = new BrowsNative();

    private volatile Task HandleTask;
    private readonly Lock HandleLocker = new();

    protected NativeLoader(string dll) {
        Dll = dll;
    }

    protected virtual void HandleLoaded() {
    }

    protected virtual void HandleFreeing() {
    }

    protected void Try(Func<int> result) {
        ArgumentNullException.ThrowIfNull(result);
        var error = result();
        if (error != 0) {
            throw new NativeErrorException(error);
        }
    }

    public string Dll { get; }
    public IntPtr Handle { get; private set; }
    public Exception Error { get; private set; }

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
