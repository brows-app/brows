using Domore.Logs;
using Domore.Runtime.InteropServices;
using Domore.Runtime.InteropServices.ComTypes;
using Domore.Runtime.Win32;
using Domore.Threading;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Extensions;

    internal class Win32FileOperator : Operator {
        private static readonly ILog Log = Logging.For(typeof(Win32FileOperator));

        private async Task<bool> Delete(FileOperationState state, CancellationToken cancellationToken) {
            var fop = state.FileOperation;
            var delete = Payload?.DeleteEntries?.ToList();
            if (delete == null || delete.Count == 0) {
                return false;
            }
            foreach (var item in delete) {
                var file = item.File;
                using (var shItemW = new ShellItemWrapper(file)) {
                    var shItem = shItemW.ShellItem;
                    var
                    hr = fop.DeleteItem(shItem, null);
                    hr.ThrowOnError();
                }
            }
            return await Task.FromResult(true);
        }

        private async Task<bool> Rename(FileOperationState state, CancellationToken cancellationToken) {
            var fop = state.FileOperation;
            var rename = Payload?.RenameEntries?.ToList();
            if (rename == null || rename.Count == 0) {
                return false;
            }
            foreach (var item in rename) {
                var oldPath = item.File;
                var newName = item.Rename();
                using (var shItemW = new ShellItemWrapper(oldPath)) {
                    var shItem = shItemW.ShellItem;
                    var
                    hr = fop.RenameItem(shItem, newName, null);
                    hr.ThrowOnError();
                }
            }
            return await Task.FromResult(true);
        }

        private async Task<bool> Copy(FileOperationState state, CancellationToken cancellationToken) {
            var fop = state.FileOperation;
            var copyFiles = (Payload?.CopyFiles ?? Array.Empty<string>()).Select(f => new { File = f, Name = (string)null });
            var copyEntries = (Payload?.CopyEntries ?? Array.Empty<IEntry>()).Select(e => new { File = e.File, Name = e.Rename() });
            var copy = copyFiles.Concat(copyEntries).ToList();
            if (copy.Count == 0) {
                return false;
            }
            using (var shDrItemW = new ShellItemWrapper(Directory)) {
                foreach (var item in copy) {
                    var fileDir = Path.GetDirectoryName(item.File);
                    var thisDir = Directory;
                    var sameDir = await Win32Path.AreSame(thisDir, fileDir, cancellationToken);
                    if (sameDir) {
                        state.Flags |= (uint)FOF.RENAMEONCOLLISION;
                        state.Flags |= (uint)FOFX.PRESERVEFILEEXTENSIONS;
                    }
                    using (var shItemW = new ShellItemWrapper(item.File)) {
                        var shItem = shItemW.ShellItem;
                        var drItem = shDrItemW.ShellItem;
                        var
                        hr = fop.CopyItem(shItem, drItem, item.Name, null);
                        hr.ThrowOnError();
                    }
                }
            }
            return true;
        }

        private async Task<bool> Move(FileOperationState state, CancellationToken cancellationToken) {
            var fop = state.FileOperation;
            var moveFiles = (Payload?.MoveFiles ?? Array.Empty<string>()).Select(f => new { File = f, Name = (string)null });
            var moveEntries = (Payload?.MoveEntries ?? Array.Empty<IEntry>()).Select(e => new { File = e.File, Name = e.Rename() });
            var move = moveFiles.Concat(moveEntries).ToList();
            if (move.Count == 0) {
                return false;
            }
            using (var shDrItemW = new ShellItemWrapper(Directory)) {
                foreach (var item in move) {
                    using (var shItemW = new ShellItemWrapper(item.File)) {
                        var shItem = shItemW.ShellItem;
                        var drItem = shDrItemW.ShellItem;
                        var
                        hr = fop.MoveItem(shItem, drItem, item.Name, null);
                        hr.ThrowOnError();
                    }
                }
            }
            return await Task.FromResult(true);
        }

        private void Create(string file, bool directory, IShellItem drItem, IFileOperation fop) {
            if (null == fop) throw new ArgumentNullException(nameof(fop));
            var name = Path.GetFileName(file);
            var attributes = directory ? FileAttributes.Directory : FileAttributes.Normal;
            var
            hr = fop.NewItem(drItem, attributes, name, null, null);
            hr.ThrowOnError();
        }

        private async Task<bool> Create(FileOperationState state, CancellationToken cancellationToken) {
            var fop = state.FileOperation;
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
            return await Task.FromResult(true);
        }

        private async Task Operate(IOperationProgress progress, CancellationToken cancellationToken) {
            using (var fopw = new FileOperationWrapper()) {
                var fop = fopw.FileOperation;
                var flags = Payload.Win32Flags();
                var state = new FileOperationState(fop) { Flags = flags };
                var performOperations =
                    await Copy(state, cancellationToken) |
                    await Move(state, cancellationToken) |
                    await Create(state, cancellationToken) |
                    await Delete(state, cancellationToken) |
                    await Rename(state, cancellationToken);

                if (performOperations) {
                    var
                    hr = fop.SetOperationFlags(state.Flags);
                    hr.ThrowOnError();

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
                            Log.Warn($"{nameof(Operate)} aborted");
                        }
                    }
                }
            }
        }

        protected override async Task ProtectedDeploy(CancellationToken cancellationToken) {
            var progress = Payload.NativeProgress
                ? null
                : Operation(cancellationToken, "Operation");
            await ThreadPool.Work(
                name: nameof(Operate),
                work: async cancellationToken => {
                    await Operate(progress, cancellationToken);
                },
                cancellationToken: cancellationToken);
        }

        public string Directory =>
            DirectoryInfo.FullName;

        public DirectoryInfo DirectoryInfo { get; }
        public STAThreadPool ThreadPool { get; }

        public Win32FileOperator(DirectoryInfo directoryInfo, IOperatorDeployment deployment, STAThreadPool threadPool) : base(deployment) {
            DirectoryInfo = directoryInfo ?? throw new ArgumentNullException(nameof(directoryInfo));
            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        }

        private class FileOperationState {
            public uint Flags { get; set; }
            public IFileOperation FileOperation { get; }

            public FileOperationState(IFileOperation fileOperation) {
                FileOperation = fileOperation;
            }
        }
    }
}
