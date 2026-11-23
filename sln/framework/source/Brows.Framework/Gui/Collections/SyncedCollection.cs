using System;
using System.Collections;
using System.Collections.ObjectModel;

namespace Brows.Gui.Collections;

/// <summary>
/// Provides an observable collection whose access can be coordinated across threads.
/// </summary>
/// <typeparam name="T">
/// The type of item in the collection.
/// </typeparam>
public sealed class SyncedCollection<T> : ObservableCollection<T>, ISyncedCollection {
    private readonly object Locker = new();

    /// <summary>
    /// Runs a function with coordinated access to this collection.
    /// </summary>
    /// <typeparam name="TResult">
    /// The type returned by the function.
    /// </typeparam>
    /// <param name="accessMethod">
    /// The function that reads or changes the collection.
    /// </param>
    /// <param name="writeAccess">
    /// Whether the function changes the collection.
    /// </param>
    /// <returns>
    /// The value returned by the function.
    /// </returns>
    public TResult Sync<TResult>(Func<TResult> accessMethod, bool writeAccess = true) {
        ArgumentNullException.ThrowIfNull(accessMethod);
        lock (Locker) {
            return accessMethod();
        }
    }

    /// <summary>
    /// Runs an action with coordinated access to this collection.
    /// </summary>
    /// <param name="accessMethod">
    /// The action that reads or changes the collection.
    /// </param>
    /// <param name="writeAccess">
    /// Whether the action changes the collection.
    /// </param>
    public void Sync(Action accessMethod, bool writeAccess = true) {
        ArgumentNullException.ThrowIfNull(accessMethod);
        lock (Locker) {
            accessMethod();
        }
    }

    void ISyncedCollection.Sync(IEnumerable collection, object context, Action accessMethod, bool writeAccess) {
        Sync(accessMethod, writeAccess);
    }
}
