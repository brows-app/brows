using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Gui;
    using Threading;
    using Threading.Tasks;

    internal class Win32FileSystemProvider : FileSystemProvider {
        private IconInput IconInput =>
            _IconInput ?? (
            _IconInput = new IconInput(IconStock.Unknown));
        private IconInput _IconInput;

        private StaThreadPool ThreadPool { get; }

        private Win32FileSystemProvider(DirectoryInfo info, StaThreadPool threadPool) : base(info) {
            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        }

        protected override FileSystemEntry Create(FileSystemInfo info, CancellationToken cancellationToken) {
            return new Win32FileSystemEntry(info, cancellationToken);
        }

        public override Image Icon =>
            _Icon ?? (
            _Icon = new ImageSourceProvided<IIconInput>(IconInput, new IconProvider(Info), CancellationToken.None) { Size = new ImageSize(16, 16) });
        private Image _Icon;

        public override IOperator Operator(IOperatorDeployment deployment) {
            return new Win32FileOperator(Info, deployment, ThreadPool);
        }

        public override Task<bool> CaseSensitive(CancellationToken cancellationToken) {
            return Win32Path.IsCaseSensitive(Path, cancellationToken);
        }

        private class IconProvider : IIconProvider {
            private IIconProvider Provider { get; } = Win32Gui.IconProvider;

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

        public class Factory : EntryProviderFactory, IEntryProviderFactoryExport {
            private readonly StaThreadPool ThreadPool = new StaThreadPool(nameof(Win32FileSystemProvider));

            public override async Task<IEntryProvider> CreateFor(string id, CancellationToken cancellationToken) {
                var info = await DirectoryInfoExtension.TryNewAsync(id, cancellationToken);
                if (info == null) return null;

                var exists = await info.ExistsAsync(cancellationToken);
                if (exists == false) return null;

                return new Win32FileSystemProvider(info, ThreadPool);
            }
        }
    }
}
