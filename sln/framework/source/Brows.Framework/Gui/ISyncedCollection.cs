using System;
using System.Collections;

namespace Brows.Gui;

/// <summary>
/// Coordinates access to a collection shared with a view.
/// </summary>
public interface ISyncedCollection : IEnumerable {
    /// <summary>
    /// Runs an action with coordinated access to a collection.
    /// </summary>
    /// <param name="collection">
    /// The collection being accessed.
    /// </param>
    /// <param name="context">
    /// The context supplied when collection synchronization was registered.
    /// </param>
    /// <param name="accessMethod">
    /// The action that reads or changes the collection.
    /// </param>
    /// <param name="writeAccess">
    /// Whether the action changes the collection.
    /// </param>
    void Sync(IEnumerable collection, object context, Action accessMethod, bool writeAccess);
}
