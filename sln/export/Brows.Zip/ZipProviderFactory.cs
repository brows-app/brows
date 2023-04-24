using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Exports;

    internal sealed class ZipProviderFactory : EntryProviderFactory<ZipProvider> {
        protected sealed override async Task<ZipProvider> CreateFor(string id, IPanel panel, CancellationToken cancellationToken) {
            if (id == null || id == "") {
                return null;
            }
            var zipID = await ZipID.Parse(id, this, cancellationToken);
            if (zipID == null) {
                return null;
            }
            return new ZipProvider(zipID, this);
        }

        public ICaseCorrectFile CaseCorrectFile { get; set; }
        public IIconZipFileInfo IconZipFileInfo { get; set; }
    }
}
