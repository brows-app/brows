using System;
using System.Threading.Tasks;

namespace Brows.Entries;

/// <summary>
/// Represents one item provided by a panel's provider.
/// </summary>
public interface IEntry {
    /// <summary>
    /// Occurs when files are dropped on the entry.
    /// </summary>
    event EntryDropEventHandler Drop;

    /// <summary>
    /// Occurs when the entry selection state changes.
    /// </summary>
    event EntrySelectedEventHandler Selected;

    /// <summary>
    /// Occurs after the entry has been refreshed.
    /// </summary>
    event EntryRefreshedEventHandler Refreshed;

    /// <summary>
    /// Gets the entry data value for a key.
    /// </summary>
    /// <param name="key">
    /// The data definition key.
    /// </param>
    /// <returns>
    /// The data associated with the key.
    /// </returns>
    IEntryData this[string key] { get; }

    /// <summary>
    /// Gets the entry identifier within its provider.
    /// </summary>
    string ID { get; }

    /// <summary>
    /// Gets the entry name displayed to the user.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets or sets whether the entry is selected.
    /// </summary>
    bool Select { get; set; }

    /// <summary>
    /// Refreshes the entry and invalidates its loaded data values.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the entry refresh finishes.
    /// </returns>
    Task Refresh(CancellationToken token);

    /// <summary>
    /// Gets or starts a reusable task for an entry-specific key.
    /// </summary>
    /// <typeparam name="T">
    /// The result type of the work.
    /// </typeparam>
    /// <param name="key">
    /// The key identifying the work to share.
    /// </param>
    /// <param name="factory">
    /// Starts the work when a reusable task is not available.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The task for the key. Failed or canceled work can be retried.
    /// </returns>
    Task<T> Task<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken token);
}
