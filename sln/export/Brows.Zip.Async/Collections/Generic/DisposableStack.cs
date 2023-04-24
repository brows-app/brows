using Domore.Logs;
using System;
using System.Collections.Generic;

namespace Brows.Collections.Generic {
    internal sealed class DisposableStack : IDisposable {
        private static readonly ILog Log = Logging.For(typeof(DisposableStack));

        private readonly Stack<IDisposable> Stack = new();

        private void Dispose(bool disposing) {
            if (disposing) {
                lock (Stack) {
                    for (; ; )
                    {
                        var pop = Stack.TryPop(out var disposable);
                        if (pop == false) {
                            break;
                        }
                        if (disposable == null) {
                            continue;
                        }
                        try {
                            disposable.Dispose();
                        }
                        catch (Exception ex) {
                            if (Log.Warn()) {
                                Log.Warn(ex);
                            }
                        }
                    }
                    Disposed = true;
                }
            }
        }

        public bool Disposed { get; private set; }

        public T Push<T>(T disposable) where T : IDisposable {
            if (Disposed) {
                throw new ObjectDisposedException(nameof(DisposableStack));
            }
            lock (Stack) {
                if (Disposed) {
                    throw new ObjectDisposedException(nameof(DisposableStack));
                }
                Stack.Push(disposable);
            }
            return disposable;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DisposableStack() {
            Dispose(false);
        }
    }
}
