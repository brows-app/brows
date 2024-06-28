using Domore.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Native {
    public class NativeLoader {
        private static readonly ILog Log = Logging.For(typeof(NativeLoader));
        private static readonly NativeLoader Brows = new BrowsNative();

        private readonly object Locker = new object();

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

        public NativeLoader(string dll) {
            Dll = dll;
        }

        public async Task<bool> Loaded(CancellationToken token) {
            if (this != Brows) {
                await Brows.Loaded(token).ConfigureAwait(false);
            }
            if (Handle == IntPtr.Zero) {
                try {
                    await Task
                        .Run(cancellationToken: token, action: () => {
                            if (Handle == IntPtr.Zero) {
                                lock (Locker) {
                                    if (Handle == IntPtr.Zero) {
                                        Handle = NativePath.Load(Dll);
                                        HandleLoaded();
                                    }
                                }
                            }
                        })
                        .ConfigureAwait(false);
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(Log.Join(nameof(Loaded), Dll), ex);
                    }
                    Error = ex;
                }
            }
            return Handle != IntPtr.Zero;
        }

        public async Task Freed(CancellationToken token) {
            if (Handle != IntPtr.Zero) {
                await Task
                    .Run(cancellationToken: token, action: () => {
                        if (Handle != IntPtr.Zero) {
                            lock (Locker) {
                                if (Handle != IntPtr.Zero) {
                                    HandleFreeing();
                                    NativePath.Free(Handle);
                                    Handle = IntPtr.Zero;
                                }
                            }
                        }
                    })
                    .ConfigureAwait(false);
            }
        }
    }
}
