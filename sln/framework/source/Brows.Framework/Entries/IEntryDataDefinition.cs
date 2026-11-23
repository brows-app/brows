using Brows.Commands;
using Brows.Composition;
using System;
using System.Threading.Tasks;

namespace Brows.Entries;

/// <summary>
/// Describes a data column and how to read, refresh, compare, and display its values.
/// </summary>
public interface IEntryDataDefinition {
    /// <summary>
    /// Gets the key identifying this data definition.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the optional display name for this data definition.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the optional display group for this data definition.
    /// </summary>
    string Group { get; }

    /// <summary>
    /// Gets the preferred column width. NaN lets the view choose the width.
    /// </summary>
    double Width { get; }

    /// <summary>
    /// Gets the entry type supported by this definition.
    /// </summary>
    Type EntryType { get; }

    /// <summary>
    /// Gets the horizontal alignment used to display values.
    /// </summary>
    EntryDataAlignment Alignment { get; }

    /// <summary>
    /// Gets the converter used to format values, or null to use the value directly.
    /// </summary>
    IEntryDataConverter Converter { get; }

    /// <summary>
    /// Gets the optional setting passed to the display converter.
    /// </summary>
    object ConverterParameter { get; }

    /// <summary>
    /// Gets the resource key builder for this data definition.
    /// </summary>
    IExportResourceKey ResourceKey { get; }

    /// <summary>
    /// Loads this data value for an entry.
    /// </summary>
    /// <param name="entry">
    /// The entry whose value is requested.
    /// </param>
    /// <param name="progress">
    /// Receives intermediate values while loading.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The loaded value.
    /// </returns>
    Task<object> GetValue(IEntry entry, Action<object> progress, CancellationToken token);

    /// <summary>
    /// Compares this definition's values for two entries.
    /// </summary>
    /// <param name="x">
    /// The first entry.
    /// </param>
    /// <param name="y">
    /// The second entry.
    /// </param>
    /// <returns>
    /// A negative value if x precedes y, zero if equal, or a positive value if x follows y.
    /// </returns>
    int CompareValue(IEntry x, IEntry y);

    /// <summary>
    /// Refreshes any data needed to load an entry value again.
    /// </summary>
    /// <param name="entry">
    /// The entry whose value is being refreshed.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when refresh work finishes.
    /// </returns>
    Task RefreshValue(IEntry entry, CancellationToken token);

    /// <summary>
    /// Determines whether to suggest this data key for the current command context.
    /// </summary>
    /// <param name="context">
    /// The command context requesting key suggestions.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the key may be suggested; otherwise, false.
    /// </returns>
    Task<bool> SuggestKey(ICommandContext context, CancellationToken token);
}
