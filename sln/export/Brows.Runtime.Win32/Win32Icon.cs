using Domore.Logs;
using Domore.Runtime.InteropServices;
using Domore.Runtime.Win32;
using Domore.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Brows {
    public static class Win32Icon {
        private static readonly ILog Log = Logging.For(typeof(Win32Icon));
        private static readonly Dictionary<string, object> ExtensionCache = [];
        private static readonly Dictionary<string, Task<object>> ExtensionTasks = [];
        private static readonly SemaphoreSlim ExtensionLock = new(1, 1);
        private static readonly Dictionary<SHSTOCKICONID, object> StockCache = [];
        private static readonly Dictionary<SHSTOCKICONID, Task<object>> StockTasks = [];
        private static readonly SemaphoreSlim StockLock = new(1, 1);

        private static STAThreadPool ThreadPool =>
            Win32ThreadPool.Common;

        private static async Task<object> Attempt<TArg>(TArg arg, Func<TArg, CancellationToken, Task<object>> task, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(task);
            var attempt = 1;
            for (; ; )
            {
                try {
                    return await task(arg, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) {
                    throw;
                }
                catch (Exception ex) {
                    if (attempt++ >= Attempts) {
                        if (Log.Warn()) {
                            Log.Warn(ex);
                        }
                        return null;
                    }
                    if (Log.Debug()) {
                        Log.Debug(ex);
                    }
                    await Task.Delay(AttemptDelay, token).ConfigureAwait(false);
                }
            }
        }

        private static Task<object> GetPathIconAttempt(string path, CancellationToken cancellationToken) {
            const FILE_ATTRIBUTE dwFileAttributes = FILE_ATTRIBUTE.NORMAL;
            const SHGFI uFlags = SHGFI.USEFILEATTRIBUTES | SHGFI.ICON | SHGFI.SMALLICON;
            return ThreadPool.Work(nameof(GetPathIconAttempt), cancellationToken: cancellationToken, work: () => {
                var pszPath = path;
                var psfi = new SHFILEINFOW();
                var cbFileInfo = SHFILEINFOW.Size;
                try {
                    var returnValue = shell32.SHGetFileInfoW(pszPath, dwFileAttributes, ref psfi, cbFileInfo, uFlags);
                    if (returnValue == IntPtr.Zero) {
                        throw new Win32Exception($"{nameof(shell32.SHGetFileInfoW)} error");
                    }
                    var source = Imaging.CreateBitmapSourceFromHIcon(psfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    if (source.CanFreeze) {
                        source.Freeze();
                    }
                    return (object)source;
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

        private static Task<object> GetStockIconAttempt(SHSTOCKICONID siid, CancellationToken cancellationToken) {
            const SHGSI uFlags = SHGSI.ICON | SHGSI.SMALLICON;
            return ThreadPool.Work(nameof(GetStockIconAttempt), cancellationToken: cancellationToken, work: () => {
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
                    return (object)source;
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

        private static async Task<object> Get<TKey>(TKey key, Dictionary<TKey, object> cache, Dictionary<TKey, Task<object>> tasks, SemaphoreSlim locker, Func<CancellationToken, Task<object>> factory, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(cache);
            ArgumentNullException.ThrowIfNull(tasks);
            ArgumentNullException.ThrowIfNull(locker);
            ArgumentNullException.ThrowIfNull(factory);
            if (cache.TryGetValue(key, out var value) == false) {
                await locker.WaitAsync(token).ConfigureAwait(false);
                try {
                    if (cache.TryGetValue(key, out value) == false) {
                        if (tasks.TryGetValue(key, out _) == false) {
                            tasks[key] = factory(token);
                        }
                        try {
                            cache[key] = value = await tasks[key].ConfigureAwait(false);
                        }
                        catch (Exception ex) {
                            if (Log.Debug()) {
                                Log.Debug(ex);
                            }
                            tasks.Remove(key);
                        }
                    }
                }
                finally {
                    locker.Release();
                }
            }
            return value;
        }

        public static int Attempts { get; set; } = 5;
        public static int AttemptDelay { get; set; } = 50;

        public static Task<object> Load(string path, CancellationToken token) {
            var ext = Path.GetExtension(path)?.Trim() ?? "";
            if (ext == "") {
                return Load(SHSTOCKICONID.DOCNOASSOC, token);
            }
            var key = ext.ToLower();
            var exe = key == ".exe" && path.Length > ".exe".Length;
            if (exe) {
                return Attempt(path, GetPathIconAttempt, token);
            }
            return Get(
                key: key,
                cache: ExtensionCache,
                tasks: ExtensionTasks,
                locker: ExtensionLock,
                factory: t => Attempt(key, GetPathIconAttempt, t),
                token: token);
        }

        public static Task<object> Load(SHSTOCKICONID stock, CancellationToken token) {
            return Get(
                key: stock,
                cache: StockCache,
                tasks: StockTasks,
                locker: StockLock,
                factory: t => Attempt(stock, GetStockIconAttempt, t),
                token: token);
        }
    }
}
