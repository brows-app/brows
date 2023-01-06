using Domore.Threading;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Gui;
    using Threading.Tasks;

    internal sealed class Win32FileSystemProvider : FileSystemProvider {
        private IconInput IconInput =>
            _IconInput ?? (
            _IconInput = new IconInput(IconStock.Unknown));
        private IconInput _IconInput;

        private STAThreadPool ThreadPool { get; }

        private Win32FileSystemProvider(DirectoryInfo info, STAThreadPool threadPool) : base(info) {
            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        }

        protected override FileSystemEntry Create(FileSystemInfo info, CancellationToken cancellationToken) {
            return new Win32FileSystemEntry(info, cancellationToken);
        }

        public override Image Icon =>
            _Icon ?? (
            _Icon = new ImageSourceProvided<IIconInput>(IconInput, new IconProvider(Info), CancellationToken.None) { Size = new ImageSize(16, 16) });
        private Image _Icon;

        public sealed override IOperator Operator(IOperatorDeployment deployment) {
            return new Win32FileOperator(Info, deployment, ThreadPool);
        }

        public sealed override Task<bool> CaseSensitive(CancellationToken cancellationToken) {
            return Win32Path.IsCaseSensitive(Path, cancellationToken);
        }

        private sealed class IconProvider : IIconProvider {
            private IIconProvider Provider { get; } = Win32Provider.Icon;

            public DirectoryInfo Info { get; }

            public IconProvider(DirectoryInfo info) {
                Info = info ?? throw new ArgumentNullException(nameof(info));
            }

            private async Task<IconStock> GetIconStock(CancellationToken cancellationToken) {
                var root = Info.Root;
                var rootName = root?.FullName;
                if (rootName != null) {
                    var infoName = Info.FullName;
                    if (infoName == rootName) {
                        var drives = await Async.Run(cancellationToken, () => DriveInfo.GetDrives());
                        foreach (var drive in drives) {
                            var driveName = drive?.Name;
                            if (driveName == rootName) {
                                return drive.GetIconStock();
                            }
                        }
                    }
                }
                return IconStock.Folder;
            }

            public async Task<object> GetImageSource(IIconInput input, ImageSize size, CancellationToken cancellationToken) {
                var stock = await GetIconStock(cancellationToken);
                return await Provider.GetImageSource(new IconInput(stock), size, cancellationToken);
            }
        }

        public sealed class Factory : EntryProviderFactory, IEntryProviderFactoryExport {
            private readonly STAThreadPool ThreadPool = new STAThreadPool(nameof(Win32FileSystemProvider));

            public sealed override async Task<IEntryProvider> CreateFor(string id, CancellationToken cancellationToken) {
                var info = await Async.Run(cancellationToken, () => {
                    try {
                        var info = new DirectoryInfo(id);
                        return info.Exists
                            ? info
                            : null;
                    }
                    catch {
                        return null;
                    }
                });
                return info == null
                    ? null
                    : new Win32FileSystemProvider(info, ThreadPool);
            }
        }
    }
}
