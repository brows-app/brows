using System.Threading.Tasks;

namespace Brows.Entries;

/// <summary>
/// Provides a loadable value for one entry data key.
/// </summary>
public interface IEntryData {
    /// <summary>
    /// Gets the key identifying this data value.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the latest reported value, which may be null before loading completes.
    /// Intermediate property-change notifications may be combined without delaying this value.
    /// </summary>
    object Value { get; }

    /// <summary>
    /// Gets a task that starts loading if needed and completes after the attempt's terminal
    /// value and notifications have been published, or the attempt has been superseded.
    /// </summary>
    Task Ready { get; }

    /// <summary>
    /// Invalidates the loaded value so it can be loaded again when requested.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the value is ready to be reloaded.
    /// </returns>
    Task Refresh(CancellationToken token);

    /// <summary>
    /// Compares this value with another entry data value.
    /// </summary>
    /// <param name="other">
    /// The value to compare.
    /// </param>
    /// <returns>
    /// A negative value if this value precedes the other, zero if they compare equally, or a positive value if it
    /// follows.
    /// </returns>
    int Compare(IEntryData other);
}
