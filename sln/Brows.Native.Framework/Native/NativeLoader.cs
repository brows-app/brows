using Domore.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Native {
    public abstract class NativeLoader {
        private static readonly ILog Log = Logging.For(typeof(NativeLoader));
        private static readonly NativeLoader Brows = new BrowsNative();

        private volatile Task HandleTask;
        private readonly object HandleLocker = new();

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
            if (this != Brows) {
                await Brows.Loaded(token).ConfigureAwait(false);
            }
            if (HandleTask == null) {
                lock (HandleLocker) {
                    if (HandleTask == null) {
                        HandleTask = Task.Run(cancellationToken: token, action: () => {
                            try {
                                Handle = NativePath.Load(Dll);
                                HandleLoaded();
                            }
                            catch (Exception ex) {
                                if (Log.Error()) {
                                    Log.Error(Log.Join(nameof(Loaded), Dll), ex);
                                }
                                Handle = IntPtr.Zero;
                                throw;
                            }
                        });
                    }
                }
            }
            if (Handle == IntPtr.Zero) {
                try {
                    await HandleTask.ConfigureAwait(false);
                }
                catch (Exception ex) {
                    Error = ex;
                }
            }
            return Handle != IntPtr.Zero;
        }

        public async Task Freed(CancellationToken token) {
            // TODO: Call this method somewhere and make sure it works.
            var task = default(Task);
            lock (HandleLocker) {
                if (HandleTask != null) {
                    HandleTask = task = Task.Run(cancellationToken: token, action: () => {
                        HandleFreeing();
                        NativePath.Free(Handle);
                        Handle = IntPtr.Zero;
                    });
                }
            }
            if (task != null) {
                await task.ConfigureAwait(false);
            }
        }
    }
}
