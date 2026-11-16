using Brows.Exports;
using Brows.Panels;
using Brows.Providers;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class TarProviderFactory : ProviderFactory<TarProvider> {
    protected sealed override async Task<TarProvider> CreateFor(
        string id,
        IPanel panel,
        CancellationToken cancellationToken) {
        if (string.IsNullOrEmpty(id)) {
            return null;
        }
        var tarID = await TarID.Parse(id, this, cancellationToken);
        if (tarID is null) {
            return null;
        }
        return new TarProvider(tarID, this);
    }

    public ICaseCorrectFile CaseCorrectFile { get; set; }
    public ICaseSensitiveDirectory CaseSensitiveDirectory { get; set; }
    public IIconTarFileInfo IconTarFileInfo { get; set; }
}
