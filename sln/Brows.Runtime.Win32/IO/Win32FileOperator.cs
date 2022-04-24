using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Extensions;
    using Logger;
    using Runtime.InteropServices;
    using Runtime.InteropServices.ComTypes;
    using Threading;

    internal class Win32FileOperator : Operator {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(Win32FileOperator)));
        private ILog _Log;

        private bool Delete(IFileOperation fop) {
            if (null == fop) throw new ArgumentNullException(nameof(fop));
            var delete = Payload?.DeleteEntries?.ToList();
            if (delete == null || delete.Count == 0) {
                return false;
            }
            foreach (var item in delete) {
                var file = item.File;
                var name = Path.GetFileName(file);
                using (var shItemW = new ShellItemWrapper(file)) {
                    var shItem = shItemW.ShellItem;
                    var
                    hr = fop.DeleteItem(shItem, null);
                    hr.ThrowOnError();
                }
            }
            return true;
        }

        private bool Rename(IFileOperation fop) {
            if (null == fop) throw new ArgumentNullException(nameof(fop));
            var rename = Payload?.RenameEntries?.ToList();
            if (rename == null || rename.Count == 0) {
                return false;
            }
            foreach (var item in rename) {
                var oldPath = item.File;
                var oldName = Path.GetFileName(oldPath);
                var newName = item.Rename();
                using (var shItemW = new ShellItemWrapper(oldPath)) {
                    var shItem = shItemW.ShellItem;
                    var
                    hr = fop.RenameItem(shItem, newName, null);
                    hr.ThrowOnError();
                }
            }
            return true;
        }

        private bool Copy(IFileOperation fop) {
            if (null == fop) throw new ArgumentNullException(nameof(fop));
            var copyFiles = Payload?.CopyFiles ?? Array.Empty<string>();
            var copyEntries = Payload?.CopyEntries?.Select(e => e.ID) ?? Array.Empty<string>();
            var copy = copyFiles.Concat(copyEntries).ToList();
            if (copy.Count == 0) {
                return false;
            }
            using (var shDrItemW = new ShellItemWrapper(Directory)) {
                foreach (var file in copy) {
                    var name = Path.GetFileName(file);
                    var path = Path.Combine(Directory, name);
                    using (var shItemW = new ShellItemWrapper(file)) {
                        var shItem = shItemW.ShellItem;
                        var drItem = shDrItemW.ShellItem;
                        var
                        hr = fop.CopyItem(shItem, drItem, null, null);
                        hr.ThrowOnError();
                    }
                }
            }
            return true;
        }

        private bool Move(IFileOperation fop) {
            if (null == fop) throw new ArgumentNullException(nameof(fop));
            var moveFiles = Payload?.MoveFiles ?? Array.Empty<string>();
            var moveEntries = Payload?.MoveEntries?.Select(e => e.ID) ?? Array.Empty<string>();
            var move = moveFiles.Concat(moveEntries).ToList();
            if (move.Count == 0) {
                return false;
            }
            using (var shDrItemW = new ShellItemWrapper(Directory)) {
                foreach (var file in move) {
                    var name = Path.GetFileName(file);
                    var path = Path.Combine(Directory, name);
                    using (var shItemW = new ShellItemWrapper(file)) {
                        var shItem = shItemW.ShellItem;
                        var drItem = shDrItemW.ShellItem;
                        var
                        hr = fop.MoveItem(shItem, drItem, null, null);
                        hr.ThrowOnError();
                    }
                }
            }
            return true;
        }

        private void Create(string file, bool directory, IShellItem drItem, IFileOperation fop) {
            if (null == fop) throw new ArgumentNullException(nameof(fop));
            var name = Path.GetFileName(file);
            var attributes = directory ? FileAttributes.Directory : FileAttributes.Normal;
            var
            hr = fop.NewItem(drItem, attributes, name, null, null);
            hr.ThrowOnError();
        }

        private bool Create(IFileOperation fop) {
            var files = Payload?.CreateFiles ?? Array.Empty<string>();
            var directories = Payload?.CreateDirectories ?? Array.Empty<string>();
            var items = files.Select(f => new {
                File = f,
                Directory = false
            }).Concat(directories.Select(d => new {
                File = d,
                Directory = true
            }));
            if (items.Any() == false) {
                return false;
            }
            using (var shDrItemW = new ShellItemWrapper(Directory)) {
                var drItem = shDrItemW.ShellItem;
                foreach (var item in items) {
                    Create(item.File, item.Directory, drItem, fop);
                }
            }
            return true;
        }

        private void Operate(IOperationProgress progress) {
            using (var fopw = new FileOperationWrapper()) {
                var fop = fopw.FileOperation;
                var flags = Payload.Win32Flags();
                var
                hr = fop.SetOperationFlags(flags);
                hr.ThrowOnError();

                var performOperations =
                    Copy(fop) |
                    Move(fop) |
                    Create(fop) |
                    Delete(fop) |
                    Rename(fop);

                if (performOperations) {
                    var window = Dialog?.Window;
                    if (window is IntPtr hwnd) {
                        hr = fop.SetOwnerWindow(hwnd);
                        hr.ThrowOnError();
                    }
                    var progressSinkCookie = default(uint);
                    if (progress != null) {
                        var progressSink = new Win32ProgressSink(progress);
                        hr = fop.Advise(progressSink, out progressSinkCookie);
                        hr.ThrowOnError();
                    }

                    hr = fop.PerformOperations();
                    hr.ThrowOnError();

                    if (progress != null) {
                        hr = fop.Unadvise(progressSinkCookie);
                        hr.ThrowOnError();
                    }

                    var aborted = default(bool);
                    hr = fop.GetAnyOperationsAborted(out aborted);
                    hr.ThrowOnError();

                    if (aborted) {
                        if (Log.Warn()) {
                            Log.Warn($"{nameof(Deploy)} aborted");
                        }
                    }
                }
            }
        }

        protected override async Task ProtectedDeploy(CancellationToken cancellationToken) {
            var progress = Payload.NativeProgress
                ? null
                : Operation(cancellationToken, "Operation", "{0}", "Operation");
            await ThreadPool.Work(() => Operate(progress), cancellationToken);
        }

        public string Directory =>
            DirectoryInfo.FullName;

        public DirectoryInfo DirectoryInfo { get; }
        public StaThreadPool ThreadPool { get; }

        public Win32FileOperator(DirectoryInfo directoryInfo, IOperatorDeployment deployment, StaThreadPool threadPool) : base(deployment) {
            DirectoryInfo = directoryInfo ?? throw new ArgumentNullException(nameof(directoryInfo));
            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        }
    }
}
