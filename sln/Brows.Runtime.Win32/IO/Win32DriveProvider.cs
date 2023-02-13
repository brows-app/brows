using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Gui;

    internal class Win32DriveProvider : DriveProvider {
        private IIconProvider IconProvider { get; } = Win32Provider.Icon;

        private IconInput IconInput =>
            _IconInput ?? (
            _IconInput = new IconInput(IconStock.DriveFixed));
        private IconInput _IconInput;

        protected override DriveEntry Create(DriveInfo info, CancellationToken cancellationToken) {
            return new Win32DriveEntry(info, cancellationToken);
        }

        public override ValueTask<bool> CaseSensitive(CancellationToken _) =>
            ValueTask.FromResult(false);

        public override Image Icon =>
            _Icon ?? (
            _Icon = new ImageSourceProvided<IIconInput>(IconInput, IconProvider, CancellationToken.None) { Size = new ImageSize(16, 16) });
        private Image _Icon;

        public class Factory : EntryProviderFactory, IEntryProviderFactoryExport {
            public override Task<IEntryProvider> CreateFor(string id, CancellationToken cancellationToken) {
                return id == DriveProviderID
                    ? Task.FromResult((IEntryProvider)new Win32DriveProvider())
                    : Task.FromResult((IEntryProvider)null);
            }

            public override Task<IEntryProvider> CreateDefault(CancellationToken cancellationToken) {
                return Task.FromResult((IEntryProvider)new Win32DriveProvider());
            }
        }
    }
}
