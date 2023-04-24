using Domore.Logs;
using Domore.Runtime.InteropServices;
using Domore.Runtime.Win32;
using Domore.Threading;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Brows {
    public class Win32Icon {
        private static readonly ILog Log = Logging.For(typeof(Win32Icon));

        private static readonly ConcurrentDictionary<string, object> ExtensionCache = new ConcurrentDictionary<string, object>();
        private static readonly ConcurrentDictionary<string, Task<object>> ExtensionTasks = new ConcurrentDictionary<string, Task<object>>();
        private static readonly ConcurrentDictionary<SHSTOCKICONID, object> StockCache = new ConcurrentDictionary<SHSTOCKICONID, object>();
        private static readonly ConcurrentDictionary<SHSTOCKICONID, Task<object>> StockTasks = new ConcurrentDictionary<SHSTOCKICONID, Task<object>>();

        private static STAThreadPool ThreadPool =>
            Win32ThreadPool.Common;

        private static async Task<object> Attempt<TArg>(TArg arg, CancellationToken cancellationToken, Func<TArg, CancellationToken, Task<object>> task) {
            if (null == task) throw new ArgumentNullException(nameof(task));
            var attempt = 1;
            for (; ; )
            {
                try {
                    return await task(arg, cancellationToken);
                }
                catch (Exception ex) {
                    if (ex is OperationCanceledException canceled) {
                        if (canceled.CancellationToken == cancellationToken) {
                            if (Log.Info()) {
                                Log.Info(canceled.GetType().Name);
                            }
                            return null;
                        }
                    }
                    if (attempt++ >= Attempts) {
                        if (Log.Error()) {
                            Log.Error(ex);
                        }
                        return null;
                    }
                    if (Log.Warn()) {
                        Log.Warn(ex);
                    }
                    await Task.Delay(AttemptDelay);
                }
            }
        }

        private static async Task<object> GetPathIconAttempt(string path, CancellationToken cancellationToken) {
            const FILE_ATTRIBUTE dwFileAttributes = FILE_ATTRIBUTE.NORMAL;
            const SHGFI uFlags = SHGFI.USEFILEATTRIBUTES | SHGFI.ICON | SHGFI.SMALLICON;
            return await ThreadPool.Work(nameof(GetPathIconAttempt), cancellationToken: cancellationToken, work: () => {
                var pszPath = path;
                var psfi = new SHFILEINFOW();
                var cbFileInfo = SHFILEINFOW.Size;
                try {
                    var returnValue = shell32.SHGetFileInfoW(pszPath, dwFileAttributes, ref psfi, cbFileInfo, uFlags);
                    if (returnValue == IntPtr.Zero) throw new Win32Exception($"{nameof(shell32.SHGetFileInfoW)} error");

                    var source = Imaging.CreateBitmapSourceFromHIcon(psfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    if (source.CanFreeze) {
                        source.Freeze();
                    }
                    return source;
                }
                finally {
                    var hIcon = psfi.hIcon;
                    if (hIcon != default) {
                        var success = user32.DestroyIcon(hIcon);
                        if (success == false) {
                            if (Log.Error()) {
                                Log.Error(nameof(user32.DestroyIcon));
                            }
                        }
                    }
                }
            });
        }

        private static async Task<object> GetStockIconAttempt(SHSTOCKICONID siid, CancellationToken cancellationToken) {
            const SHGSI uFlags = SHGSI.ICON | SHGSI.SMALLICON;
            return await ThreadPool.Work(nameof(GetStockIconAttempt), cancellationToken: cancellationToken, work: () => {
                var psii = new SHSTOCKICONINFO();
                psii.cbSize = SHSTOCKICONINFO.Size;
                try {
                    var
                    hr = shell32.SHGetStockIconInfo(siid, uFlags, ref psii);
                    hr.ThrowOnError();

                    var source = Imaging.CreateBitmapSourceFromHIcon(psii.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    if (source.CanFreeze) {
                        source.Freeze();
                    }
                    return source;
                }
                finally {
                    var hIcon = psii.hIcon;
                    if (hIcon != default) {
                        var success = user32.DestroyIcon(hIcon);
                        if (success == false) {
                            if (Log.Error()) {
                                Log.Error(nameof(user32.DestroyIcon));
                            }
                        }
                    }
                }
            });
        }

        public static int Attempts { get; set; } = 5;
        public static int AttemptDelay { get; set; } = 50;

        public static async Task<object> Load(string path, CancellationToken cancellationToken) {
            var ext = Path.GetExtension(path)?.Trim() ?? "";
            if (ext == "") {
                return await Load(SHSTOCKICONID.DOCNOASSOC, cancellationToken);
            }
            var key = ext.ToLower();
            var exe = key == ".exe" && path.Length > ".exe".Length;
            if (exe) {
                return await Attempt(path, cancellationToken, GetPathIconAttempt);
            }
            var cache = ExtensionCache;
            if (cache.TryGetValue(key, out var value) == false) {
                var tasks = ExtensionTasks;
                if (tasks.TryGetValue(key, out var task) == false) {
                    tasks[key] = task = Attempt(key, cancellationToken, GetPathIconAttempt);
                }
                cache[key] = value = await task;
            }
            return value;
        }

        public static async Task<object> Load(SHSTOCKICONID stock, CancellationToken cancellationToken) {
            var key = stock;
            var cache = StockCache;
            if (cache.TryGetValue(key, out var value) == false) {
                var tasks = StockTasks;
                if (tasks.TryGetValue(key, out var task) == false) {
                    tasks[key] = task = Attempt(stock, cancellationToken, GetStockIconAttempt);
                }
                cache[key] = value = await task;
            }
            return value;
        }
    }
}
