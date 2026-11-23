using Brows.Entries;
using Brows.Providers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Panels;

/// <summary>
/// Provides access to a panel's current provider, entries, view, and credentials.
/// </summary>
public interface IPanel {
    /// <summary>
    /// Gets the panel's zero-based column position.
    /// </summary>
    int Column { get; }

    /// <summary>
    /// Tries to get the panel's entry view.
    /// </summary>
    /// <param name="view">
    /// Receives the entry view when available.
    /// </param>
    /// <returns>
    /// True if an entry view is available; otherwise, false.
    /// </returns>
    bool HasView(out IEntryDataView view);

    /// <summary>
    /// Tries to get the panel's selected entries.
    /// </summary>
    /// <param name="entries">
    /// Receives the selected entries when available.
    /// </param>
    /// <returns>
    /// True if the requested entries are available; otherwise, false.
    /// </returns>
    bool HasSelection(out IReadOnlySet<IEntry> entries);

    /// <summary>
    /// Tries to get the panel's selected entries of the requested type.
    /// </summary>
    /// <typeparam name="TEntry">
    /// The required entry type.
    /// </typeparam>
    /// <param name="entries">
    /// Receives selected entries of the requested type when available.
    /// </param>
    /// <returns>
    /// True if at least one selected entry has the requested type; otherwise, false.
    /// </returns>
    bool HasSelection<TEntry>(out IReadOnlySet<TEntry> entries) where TEntry : class, IEntry;

    /// <summary>
    /// Tries to get the panel's current entry.
    /// </summary>
    /// <param name="current">
    /// Receives the current entry when available.
    /// </param>
    /// <returns>
    /// True if a current entry is available; otherwise, false.
    /// </returns>
    bool HasEntry(out IEntry current);

    /// <summary>
    /// Tries to get the panel's entries.
    /// </summary>
    /// <param name="entries">
    /// Receives the entries when available.
    /// </param>
    /// <returns>
    /// True if the collection is nonempty; otherwise, false.
    /// </returns>
    bool HasEntries(out IReadOnlyList<IEntry> entries);

    /// <summary>
    /// Tries to get the panel's entries of the requested type.
    /// </summary>
    /// <typeparam name="TEntry">
    /// The required entry type.
    /// </typeparam>
    /// <param name="entries">
    /// Receives entries of the requested type when available.
    /// </param>
    /// <returns>
    /// True if at least one entry has the requested type; otherwise, false.
    /// </returns>
    bool HasEntries<TEntry>(out IReadOnlyList<TEntry> entries) where TEntry : class, IEntry;

    /// <summary>
    /// Tries to get the panel's current provider.
    /// </summary>
    /// <param name="provider">
    /// Receives the provider when available.
    /// </param>
    /// <returns>
    /// True if a provider is available; otherwise, false.
    /// </returns>
    bool HasProvider(out IProvider provider);

    /// <summary>
    /// Tries to get the panel's current provider.
    /// </summary>
    /// <typeparam name="TProvider">
    /// The required provider type.
    /// </typeparam>
    /// <param name="provider">
    /// Receives the provider when available.
    /// </param>
    /// <returns>
    /// True if a provider of the requested type is available; otherwise, false.
    /// </returns>
    bool HasProvider<TProvider>(out TProvider provider) where TProvider : class, IProvider;

    /// <summary>
    /// Tries to get the current provider and one of its services.
    /// </summary>
    /// <typeparam name="TService">
    /// The required provider service type.
    /// </typeparam>
    /// <param name="provider">
    /// Receives the current provider.
    /// </param>
    /// <param name="service">
    /// Receives the requested provider service.
    /// </param>
    /// <returns>
    /// True if both the provider and service are available; otherwise, false.
    /// </returns>
    bool HasProviderService<TService>(out IProvider provider, out TService service) where TService : class, IProviderExport;

    /// <summary>
    /// Tries to get the panel native window.
    /// </summary>
    /// <param name="native">
    /// Receives the platform-specific window.
    /// </param>
    /// <returns>
    /// True if a native window is available; otherwise, false.
    /// </returns>
    bool HasWindow(out object native);

    /// <summary>
    /// Opens a provider in this panel.
    /// </summary>
    /// <param name="id">
    /// The identifier of the location or content to open.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the provider was opened; otherwise, false.
    /// </returns>
    Task<bool> Provide(string id, CancellationToken token);

    /// <summary>
    /// Retrieves stored credentials or asks the user for them.
    /// </summary>
    /// <typeparam name="TSecret">
    /// The kind of credentials to retrieve.
    /// </typeparam>
    /// <param name="key">
    /// The key used to identify stored credentials.
    /// </param>
    /// <param name="promptFormat">
    /// The format string for the user prompt.
    /// </param>
    /// <param name="promptArgs">
    /// The values to insert into the prompt.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The credentials, or the default value if none are supplied.
    /// </returns>
    Task<TSecret> GetSecret<TSecret>(string key,
                                     string promptFormat,
                                     IEnumerable<string> promptArgs,
                                     CancellationToken token)
    where TSecret : IPanelSecret;
}
