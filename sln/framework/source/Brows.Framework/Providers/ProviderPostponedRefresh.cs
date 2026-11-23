using System.Diagnostics;
using System.Threading.Tasks;

namespace Brows.Providers;

/// <summary>
/// Limits repeated provider refreshes using the provider's configured interval.
/// </summary>
public sealed class ProviderPostponedRefresh {
    private bool Refreshed;
    private readonly Stopwatch Stopwatch;

    /// <summary>
    /// Gets the provider to refresh.
    /// </summary>
    public EntryProvider Provider { get; }

    /// <summary>
    /// Initializes refresh timing for a provider.
    /// </summary>
    /// <param name="provider">
    /// The provider to refresh.
    /// </param>
    public ProviderPostponedRefresh(EntryProvider provider) {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Refreshes the provider when its interval has elapsed or a final refresh is needed.
    /// </summary>
    /// <param name="final">
    /// True to finish with a refresh unless the preceding call already refreshed the provider.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the provider was refreshed; otherwise, false.
    /// </returns>
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

    /// <summary>
    /// Refreshes the provider if its configured refresh interval has elapsed.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the provider was refreshed; otherwise, false.
    /// </returns>
    public Task<bool> Work(CancellationToken token) {
        return Work(final: false, token);
    }
}
