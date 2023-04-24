using Domore.Logs;
using Domore.Runtime.InteropServices;
using Domore.Runtime.InteropServices.ComTypes;
using Domore.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace Brows {
    using Config;

    public sealed class Win32FileOperation {
        private static readonly ILog Log = Logging.For(typeof(Win32FileOperation));

        private uint Flags;
        private IFileOperation FileOperation;
        private IOperationProgress OperationProgress;
        private CancellationToken CancellationToken;

        private ShellItemWrapper DirectoryWrap =>
            _DirectoryWrap ?? (
            _DirectoryWrap = new ShellItemWrapper(Directory));
        private ShellItemWrapper _DirectoryWrap;

        private STAThreadPool ThreadPool {
            get => _ThreadPool ?? (_ThreadPool = Win32ThreadPool.Common);
            set => _ThreadPool = value;
        }
        private STAThreadPool _ThreadPool;

        private Win32FileOperationConfig Config {
            get => _Config ?? (_Config = new());
            set => _Config = value;
        }
        private Win32FileOperationConfig _Config;

        private bool Iterate<T>(IReadOnlyList<T> list, Action<T> act) {
            if (list == null) return false;
            if (list.Count == 0) return false;
            var acted = false;
            foreach (var item in list) {
                if (item != null) {
                    act?.Invoke(item);
                    acted = true;
                }
            }
            return acted;
        }

        private bool Delete() {
            return Iterate(DeleteFiles, item => {
                var path = Path.Combine(Directory, item.Name);
                using (var itemWrap = new ShellItemWrapper(path)) {
                    var
                    hr = FileOperation.DeleteItem(itemWrap.ShellItem, null);
                    hr.ThrowOnError();
                }
            });
        }

        private bool Rename() {
            return Iterate(RenameFiles, item => {
                var oldPath = Path.Combine(Directory, item.OldName);
                var newName = item.NewName;
                using (var itemWrap = new ShellItemWrapper(oldPath)) {
                    var
                    hr = FileOperation.RenameItem(itemWrap.ShellItem, newName, null);
                    hr.ThrowOnError();
                }
            });
        }

        private bool Copy() {
            return Iterate(CopyFiles, item => {
                var path = item.Path;
                var fileDir = Path.GetDirectoryName(path);
                using (var itemWrap = new ShellItemWrapper(path)) {
                    var
                    hr = FileOperation.CopyItem(itemWrap.ShellItem, DirectoryWrap.ShellItem, null, null);
                    hr.ThrowOnError();
                }
            });
        }

        private bool Move() {
            return Iterate(MoveFiles, item => {
                using (var itemWrap = new ShellItemWrapper(item.Path)) {
                    var
                    hr = FileOperation.MoveItem(itemWrap.ShellItem, DirectoryWrap.ShellItem, null, null);
                    hr.ThrowOnError();
                }
            });
        }

        private bool Create() {
            return Iterate(CreateFiles, item => {
                var
                hr = FileOperation.NewItem(DirectoryWrap.ShellItem, item.Attributes, item.Name, null, null);
                hr.ThrowOnError();
            });
        }

        private IntPtr Window() {
            var app = Application.Current;
            if (app != null) {
                return app.Dispatcher.Invoke(() => {
                    var main = app?.MainWindow;
                    if (main != null) {
                        var help = new WindowInteropHelper(main);
                        return help.Handle;
                    }
                    return IntPtr.Zero;
                });
            }
            return IntPtr.Zero;
        }

        private bool Work() {
            using (var fopw = new FileOperationWrapper()) {
                FileOperation = fopw.FileOperation;
                Flags = Config.Flags(this);
                var performOperations =
                    Copy() |
                    Move() |
                    Create() |
                    Delete() |
                    Rename();
                if (performOperations == false) {
                    return false;
                }
                var
                hr = FileOperation.SetOperationFlags(Flags);
                hr.ThrowOnError();

                var window = Window();
                if (window != IntPtr.Zero) {
                    hr = FileOperation.SetOwnerWindow(window);
                    hr.ThrowOnError();
                }
                var progressSinkCookie = default(uint);
                var progressSink = default(Win32ProgressSink);
                var progressNative = !Config.Silent;
                if (progressNative == false) {
                    progressSink = new Win32ProgressSink(OperationProgress, CancellationToken);
                    hr = FileOperation.Advise(progressSink, out progressSinkCookie);
                    hr.ThrowOnError();
                }
                hr = FileOperation.PerformOperations();
                hr.ThrowOnError();

                if (progressSink != null) {
                    hr = FileOperation.Unadvise(progressSinkCookie);
                    hr.ThrowOnError();
                }
                var aborted = default(bool);
                hr = FileOperation.GetAnyOperationsAborted(out aborted);
                hr.ThrowOnError();

                if (aborted) {
                    if (Log.Warn()) {
                        Log.Warn($"{nameof(Work)} aborted");
                    }
                }
                return true;
            }
        }

        public List<CopyFile> CopyFiles {
            get => _CopyFiles ?? (_CopyFiles = new());
            set => _CopyFiles = value;
        }
        private List<CopyFile> _CopyFiles;

        public List<MoveFile> MoveFiles {
            get => _MoveFiles ?? (_MoveFiles = new());
            set => _MoveFiles = value;
        }
        private List<MoveFile> _MoveFiles;

        public List<DeleteFile> DeleteFiles {
            get => _DeleteFiles ?? (_DeleteFiles = new());
            set => _DeleteFiles = value;
        }
        private List<DeleteFile> _DeleteFiles;

        public List<CreateFile> CreateFiles {
            get => _CreateFiles ?? (_CreateFiles = new());
            set => _CreateFiles = value;
        }
        private List<CreateFile> _CreateFiles;

        public List<RenameFile> RenameFiles {
            get => _RenameFiles ?? (_RenameFiles = new());
            set => _RenameFiles = value;
        }
        private List<RenameFile> _RenameFiles;

        public bool? RecycleOnDelete { get; set; }
        public bool? PreserveFileExtensions { get; set; }
        public bool? RenameOnCollision { get; set; }

        public string Directory { get; }

        public Win32FileOperation(string directory) {
            Directory = directory;
        }

        public async Task<bool> Operate(IOperationProgress operationProgress, CancellationToken cancellationToken) {
            var agent = new Win32FileOperation(Directory) {
                CancellationToken = cancellationToken,
                Config = await Configure.File<Win32FileOperationConfig>().Load(cancellationToken),
                CopyFiles = _CopyFiles?.ToList(),
                CreateFiles = _CreateFiles?.ToList(),
                DeleteFiles = _DeleteFiles?.ToList(),
                MoveFiles = _MoveFiles?.ToList(),
                RenameFiles = _RenameFiles?.ToList(),
                PreserveFileExtensions = PreserveFileExtensions,
                RecycleOnDelete = RecycleOnDelete,
                RenameOnCollision = RenameOnCollision,
                OperationProgress = operationProgress,
                ThreadPool = ThreadPool
            };
            try {
                return await ThreadPool.Work(
                    name: nameof(Win32FileOperation),
                    work: agent.Work,
                    cancellationToken: cancellationToken);
            }
            finally {
                var directoryWrap = agent._DirectoryWrap;
                if (directoryWrap != null) {
                    directoryWrap.Dispose();
                }
            }
        }

        public class CopyFile {
            public string Path { get; set; }
        }

        public class MoveFile {
            public string Path { get; set; }
        }

        public class DeleteFile {
            public string Name { get; set; }
        }

        public class CreateFile {
            public string Name { get; set; }
            public FileAttributes Attributes { get; set; }
        }

        public class RenameFile {
            public string OldName { get; set; }
            public string NewName { get; set; }
        }
    }
}
