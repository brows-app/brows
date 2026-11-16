using Brows.Entries;

namespace Brows.Providers;

/// <summary>
/// A provider whose content is a list of entries.
/// </summary>
public interface IEntryProvider : IProvider {
    /// <summary>
    /// Gets the data definitions of the provided entries.
    /// </summary>
    IEntryDataDefinitionSet Data { get; }

    /// <summary>
    /// Gets the observation of the provided entries.
    /// </summary>
    IEntryObservation Observation { get; }
}
