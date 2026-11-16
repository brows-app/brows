using Brows.Exports;
using Brows.Panels;
using Brows.Providers;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SevenZipProviderFactory : ProviderFactory<SevenZipProvider> {
    protected sealed override async Task<SevenZipProvider> CreateFor(
        string id,
        IPanel panel,
        CancellationToken cancellationToken) {
        if (string.IsNullOrEmpty(id)) {
            return null;
        }
        var sevenZipID = await SevenZipID.Parse(id, this, cancellationToken);
        if (sevenZipID is null) {
            return null;
        }
        return new SevenZipProvider(sevenZipID, this);
    }

    public ICaseCorrectFile CaseCorrectFile { get; set; }
    public ICaseSensitiveDirectory CaseSensitiveDirectory { get; set; }
    public IIconSevenZipFileInfo IconSevenZipFileInfo { get; set; }
}
