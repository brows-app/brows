using System.Diagnostics;
using System.Threading.Tasks;

namespace Brows.Providers;

public sealed class ProviderPostponedRefresh {
    private bool Refreshed;
    private readonly Stopwatch Stopwatch;

    public EntryProvider Provider { get; }

    public ProviderPostponedRefresh(EntryProvider provider) {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Stopwatch = Stopwatch.StartNew();
    }

    public async Task<bool> Work(bool final, CancellationToken token) {
        if (final) {
            if (Refreshed) {
                return false;
            }
        }
        var refresh = final || Stopwatch.Elapsed > Provider.Config?.PostponedRefresh?.Interval;
        if (refresh) {
            var provider = (IProvider)Provider;
            await provider.Refresh(token);
            Stopwatch.Restart();
            return Refreshed = true;
        }
        return Refreshed = false;
    }

    public Task<bool> Work(CancellationToken token) {
        return Work(final: false, token);
    }
}
