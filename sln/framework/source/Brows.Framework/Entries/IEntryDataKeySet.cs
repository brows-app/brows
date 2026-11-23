using System.Collections.Generic;

namespace Brows.Entries;

/// <summary>
/// Resolves entry data keys and their translated aliases.
/// </summary>
public interface IEntryDataKeySet {
    /// <summary>
    /// Resolves a key or translated alias, ignoring letter case.
    /// </summary>
    /// <param name="alias">
    /// The key or alias to resolve.
    /// </param>
    /// <returns>
    /// The data key, or null when no key matches.
    /// </returns>
    string Lookup(string alias);

    /// <summary>
    /// Finds data keys whose aliases contain the supplied text, ignoring letter case.
    /// </summary>
    /// <param name="part">
    /// The portion of an alias to find.
    /// </param>
    /// <returns>
    /// The matching data keys.
    /// </returns>
    IReadOnlySet<string> Possible(string part);

    /// <summary>
    /// Gets the accepted aliases for each data key.
    /// </summary>
    /// <returns>
    /// The data keys paired with their aliases.
    /// </returns>
    IReadOnlyDictionary<string, IReadOnlySet<string>> Alias();
}
