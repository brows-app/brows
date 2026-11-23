namespace Brows.Entries;

/// <summary>
/// Controls which data columns are shown and how entries are sorted.
/// </summary>
public interface IEntryDataView {
    /// <summary>
    /// Gets the current sort keys and directions.
    /// </summary>
    IEntrySorting Sorting { get; }

    /// <summary>
    /// Determines whether a data column is included in the view.
    /// </summary>
    /// <param name="name">
    /// The data key or alias.
    /// </param>
    /// <returns>
    /// True if the column is included; otherwise, false.
    /// </returns>
    bool Has(string name);

    /// <summary>
    /// Removes all displayed data columns.
    /// </summary>
    void Clear();

    /// <summary>
    /// Reapplies the configured columns and sorting, using default columns if the column list is empty.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Restores the default columns and sorting.
    /// </summary>
    void Reset();

    /// <summary>
    /// Applies sort settings after resolving data key aliases.
    /// </summary>
    /// <param name="sorting">
    /// The sort keys and directions, or null to clear sorting.
    /// </param>
    /// <returns>
    /// The resolved keys in input order, with null for unknown keys; null if sorting is cleared.
    /// </returns>
    string[] Sort(IEntrySorting sorting);

    /// <summary>
    /// Adds data columns after resolving their keys or aliases.
    /// </summary>
    /// <param name="names">
    /// The column keys or aliases. The array is updated with the resolved keys.
    /// </param>
    /// <returns>
    /// The resolved keys in input order, with null for names that could not be handled.
    /// </returns>
    string[] Add(params string[] names);

    /// <summary>
    /// Removes data columns after resolving their keys or aliases.
    /// </summary>
    /// <param name="names">
    /// The column keys or aliases. The array is updated with the resolved keys.
    /// </param>
    /// <returns>
    /// The resolved keys in input order, with null for names that could not be handled.
    /// </returns>
    string[] Remove(params string[] names);

    /// <summary>
    /// Sizes data columns to fit their contents.
    /// </summary>
    /// <param name="names">
    /// The column keys or aliases, or no names to fit all configured columns.
    /// </param>
    /// <returns>
    /// The resolved keys in input order, with null for names that could not be handled.
    /// </returns>
    string[] Fit(params string[] names);
}
