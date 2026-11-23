using System.Collections.Generic;

namespace Brows.Entries;

/// <summary>
/// Associates ordered data keys with sort directions. A null direction leaves that key inactive.
/// </summary>
public interface IEntrySorting : IReadOnlyDictionary<string, EntrySortDirection?> {
    /// <summary>
    /// Creates sorting settings with an updated direction for one key.
    /// </summary>
    /// <param name="key">
    /// The data key to update.
    /// </param>
    /// <param name="value">
    /// The sort direction, or null to leave the key inactive.
    /// </param>
    /// <returns>
    /// The updated settings, leaving this instance unchanged.
    /// </returns>
    IEntrySorting Change(string key, EntrySortDirection? value);
}
