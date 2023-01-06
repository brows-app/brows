using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Runtime.InteropServices {
    using Logs;
    using Threading;
    using Win32;

    internal class PreviewWorker {
        private static long ID;
        private static readonly ILog Log = Logging.For(typeof(PreviewWorker));

        private STAThreadPool ThreadPool =>
            _ThreadPool ?? (
            _ThreadPool = new STAThreadPool(nameof(PreviewWorker) + "-" + Interlocked.Increment(ref ID)) {
                WorkerCountMax = 1,
                WorkerCountMin = 0
            });
        private STAThreadPool _ThreadPool;

        private PreviewWorkerCLSID CLSID =>
            _CLSID ?? (
            _CLSID = new PreviewWorkerCLSID(ThreadPool));
        private PreviewWorkerCLSID _CLSID;

        private object State;
        private PreviewHandlerWrapper Handler;
        private readonly object Locker = new object();

        private void Unload() {
            Try(nameof(Handler.Unload), () => Handler?.Unload(), swallow: true);
            Try(nameof(Handler.Dispose), () => Handler?.Dispose(), swallow: true);
            Handler = null;
        }

        private void Try(string name, Action action, bool swallow = false) {
            if (action != null) {
                try {
                    action();
                }
                catch (Exception ex) {
                    if (swallow) {
                        if (Log.Info()) {
                            Log.Info(name, ex);
                        }
                    }
                    else {
                        if (Log.Debug()) {
                            Log.Debug(name, ex);
                        }
                        throw;
                    }
                }
            }
        }

        private async Task<Guid> ChangeCLSID(string extension, CancellationToken cancellationToken) {
            var clsid = await CLSID.For(extension, cancellationToken);
            var change = new PreviewWorkerCLSIDChangedEventArgs(extension, clsid);
            CLSIDChanged?.Invoke(this, change);
            return change.Override.HasValue
                ? change.Override.Value
                : change.CLSID;
        }

        public event PreviewWorkerCLSIDChangedEventHandler CLSIDChanged;

        public async Task Change(RECT? rect, CancellationToken cancellationToken) {
            if (rect == null) {
                return;
            }
            var handler = Handler;
            if (handler == null) {
                return;
            }
            await ThreadPool.Work(nameof(handler.SetRect), cancellationToken: cancellationToken, work: () => {
                if (handler != Handler) {
                    return;
                }
                lock (Locker) {
                    if (handler != Handler) {
                        return;
                    }
                    Try(nameof(handler.SetRect), () => handler.SetRect(rect.Value));
                }
            });
        }

        public async Task Unload(CancellationToken cancellationToken) {
            await ThreadPool.Work(nameof(Unload), cancellationToken: cancellationToken, work: () => {
                lock (Locker) {
                    Unload();
                    ThreadPool.Empty();
                }
            });
        }

        public async Task<bool> Start(string file, IntPtr? hwnd, RECT? rect, CancellationToken cancellationToken) {
            if (file == null) {
                return false;
            }
            if (rect == null) {
                return false;
            }
            if (hwnd == null || hwnd == IntPtr.Zero) {
                return false;
            }
            var ext = Path.GetExtension(file);
            var clsid = await ChangeCLSID(ext, cancellationToken);
            if (clsid == Guid.Empty) {
                return false;
            }
            var state = State = new object();
            return await ThreadPool.Work(nameof(Handler.DoPreview), cancellationToken: cancellationToken, work: () => {
                if (state != State) {
                    return false;
                }
                lock (Locker) {
                    if (state != State) {
                        return false;
                    }
                    Unload();
                    var handler = Handler = new PreviewHandlerWrapper(clsid);
                    try {
                        Try(nameof(handler.Initialize), () => handler.Initialize(file));
                        cancellationToken.ThrowIfCancellationRequested();

                        Try(nameof(handler.SetWindow), () => handler.SetWindow(hwnd.Value, rect.Value));
                        cancellationToken.ThrowIfCancellationRequested();

                        Try(nameof(handler.DoPreview), () => handler.DoPreview());
                        cancellationToken.ThrowIfCancellationRequested();

                        Try(nameof(handler.SetRect), () => handler.SetRect(rect.Value));
                        cancellationToken.ThrowIfCancellationRequested();

                        //handler.SetSite(new PreviewHandlerFrame());
                        //handler.Initialize(file);
                        //handler.SetBackgroundColor(unchecked((uint)area.BackgroundColor));
                        //handler.SetTextColor(unchecked((uint)area.ForegroundColor));
                        //handler.SetWindow(hwnd, rect);
                        //handler.DoPreview();
                        //handler.SetRect(rect);
                        return true;
                    }
                    catch {
                        Unload();
                        throw;
                    }
                }
            });
        }
    }
}
