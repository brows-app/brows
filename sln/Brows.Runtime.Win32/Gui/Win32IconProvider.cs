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

namespace Brows.Gui {
    internal class Win32IconProvider : IconProvider {
        private static readonly ILog Log = Logging.For(typeof(Win32IconProvider));

        private readonly ConcurrentDictionary<string, object> ExtensionCache = new ConcurrentDictionary<string, object>();
        private readonly ConcurrentDictionary<string, Task<object>> ExtensionTasks = new ConcurrentDictionary<string, Task<object>>();
        private readonly ConcurrentDictionary<IconStock, object> StockCache = new ConcurrentDictionary<IconStock, object>();
        private readonly ConcurrentDictionary<IconStock, Task<object>> StockTasks = new ConcurrentDictionary<IconStock, Task<object>>();

        private async Task<object> Attempt<TArg>(TArg arg, CancellationToken cancellationToken, Func<TArg, CancellationToken, Task<object>> task) {
            if (null == task) throw new ArgumentNullException(nameof(task));
            var attempt = 1;
            for (; ; ) {
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

        private async Task<object> GetEntryIconAttempt(string path, CancellationToken cancellationToken) {
            const FILE_ATTRIBUTE dwFileAttributes = FILE_ATTRIBUTE.NORMAL;
            const SHGFI uFlags = SHGFI.USEFILEATTRIBUTES | SHGFI.ICON | SHGFI.SMALLICON;
            return await ThreadPool.Work(nameof(GetEntryIconAttempt), cancellationToken: cancellationToken, work: () => {
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
                    if (hIcon != default(IntPtr)) {
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

        private static SHSTOCKICONID GetStockIconID(IconStock icon) {
            switch (icon) {
                case IconStock.Delete: return SHSTOCKICONID.SIID_DELETE;
                case IconStock.DriveCD: return SHSTOCKICONID.SIID_DRIVECD;
                case IconStock.DriveFixed: return SHSTOCKICONID.SIID_DRIVEFIXED;
                case IconStock.DriveNetwork: return SHSTOCKICONID.SIID_DRIVENET;
                case IconStock.DriveNetworkDisabled: return SHSTOCKICONID.SIID_DRIVENETDISABLED;
                case IconStock.DriveRam: return SHSTOCKICONID.SIID_DRIVERAM;
                case IconStock.DriveRemovable: return SHSTOCKICONID.SIID_DRIVEREMOVE;
                case IconStock.DriveUnknown: return SHSTOCKICONID.SIID_DRIVEUNKNOWN;
                case IconStock.Find: return SHSTOCKICONID.SIID_FIND;
                case IconStock.Folder: return SHSTOCKICONID.SIID_FOLDER;
                case IconStock.Help: return SHSTOCKICONID.SIID_HELP;
                case IconStock.Rename: return SHSTOCKICONID.SIID_RENAME;
                case IconStock.Trash: return SHSTOCKICONID.SIID_RECYCLER;
                case IconStock.ZipFile: return SHSTOCKICONID.SIID_ZIPFILE;
            }
            return SHSTOCKICONID.SIID_DOCNOASSOC;
        }

        private async Task<object> GetStockIconAttempt(IconStock id, CancellationToken cancellationToken) {
            const SHGSI uFlags = SHGSI.ICON | SHGSI.SMALLICON;
            return await ThreadPool.Work(nameof(GetStockIconAttempt), cancellationToken: cancellationToken, work: () => {
                var psii = new SHSTOCKICONINFO();
                psii.cbSize = SHSTOCKICONINFO.Size;
                try {
                    var siid = GetStockIconID(id);
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
                    if (hIcon != default(IntPtr)) {
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

        private async Task<object> GetEntryIcon(string id, CancellationToken cancellationToken) {
            var ext = Path.GetExtension(id)?.Trim() ?? "";
            if (ext == "") {
                return await GetStockIcon(IconStock.Unknown, cancellationToken);
            }
            var key = ext.ToLower();
            var exe = key == ".exe";
            if (exe) {
                return await Attempt(id, cancellationToken, GetEntryIconAttempt);
            }
            var cache = ExtensionCache;
            if (cache.TryGetValue(key, out var value) == false) {
                var tasks = ExtensionTasks;
                if (tasks.TryGetValue(key, out var task) == false) {
                    tasks[key] = task = Attempt(key, cancellationToken, GetEntryIconAttempt);
                }
                cache[key] = value = await task;
            }
            return value;
        }

        private async Task<object> GetStockIcon(IconStock id, CancellationToken cancellationToken) {
            var key = id;
            var cache = StockCache;
            if (cache.TryGetValue(key, out var value) == false) {
                var tasks = StockTasks;
                if (tasks.TryGetValue(key, out var task) == false) {
                    tasks[key] = task = Attempt(id, cancellationToken, GetStockIconAttempt);
                }
                cache[key] = value = await task;
            }
            return value;
        }

        public int Attempts { get; set; } = 5;
        public int AttemptDelay { get; set; } = 50;

        public STAThreadPool ThreadPool { get; }

        public Win32IconProvider(STAThreadPool threadPool) {
            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        }

        public override Task<object> GetImageSource(IIconInput input, ImageSize size, CancellationToken cancellationToken) {
            if (null == input) throw new ArgumentNullException(nameof(input));
            return string.IsNullOrWhiteSpace(input.ID)
                ? GetStockIcon(input.Stock, cancellationToken)
                : GetEntryIcon(input.ID, cancellationToken);
        }
    }
}
