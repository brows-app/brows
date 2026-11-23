using Brows.Panels;
using Domore.Logs;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Providers;

/// <summary>
/// Tries a group of factories to find a provider for an identifier.
/// </summary>
public sealed class ProviderFactorySet {
    private static readonly ILog Log = Logging.For(typeof(ProviderFactorySet));

    /// <summary>
    /// Gets the non-null factories in this set.
    /// </summary>
    public IEnumerable<IProviderFactory> Items { get; }

    /// <summary>
    /// Initializes a set of provider factories, ignoring null entries.
    /// </summary>
    /// <param name="items">
    /// The factories to try.
    /// </param>
    public ProviderFactorySet(IEnumerable<IProviderFactory> items) {
        ArgumentNullException.ThrowIfNull(items);
        Items = items.Where(item => item != null).ToList();
    }

    /// <summary>
    /// Requests a provider from every factory at once and returns the first non-null provider to complete.
    /// </summary>
    /// <param name="id">
    /// The provider identifier to open.
    /// </param>
    /// <param name="host">
    /// The panel that will host the provider.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The first provider returned, or null when no factory returns one.
    /// </returns>
    public async Task<IProvider> CreateFor(string id, IPanel host, CancellationToken cancellationToken) {
        var tasks = Items
            .Select(factory => factory.CreateFor(id, host, cancellationToken))
            .Where(task => task != null)
            .ToList();
        for (; ; ) {
            if (tasks.Count == 0) {
                return null;
            }
            var task = await Task.WhenAny(tasks);
            var provider = default(IProvider);
            try {
                provider = await task;
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(ex);
                }
            }
            if (provider != null) {
                return provider;
            }
            tasks.Remove(task);
        }
    }
}
