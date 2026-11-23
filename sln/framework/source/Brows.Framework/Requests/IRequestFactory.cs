using Brows.Composition;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Brows.Requests;

/// <summary>
/// Creates a view action whose availability can depend on property changes.
/// </summary>
public interface IRequestFactory : IExport {
    /// <summary>
    /// Creates an action that a view can invoke and query for availability.
    /// </summary>
    /// <param name="execute">
    /// Performs the action with the supplied parameter.
    /// </param>
    /// <param name="canExecute">
    /// Determines whether the action is available for the supplied parameter.
    /// </param>
    /// <param name="owner">
    /// The object whose property changes may affect availability.
    /// </param>
    /// <param name="dependencies">
    /// The names of properties that affect availability.
    /// </param>
    /// <returns>
    /// The platform-specific action object.
    /// </returns>
    object Create(Action<object> execute,
                  Func<object, bool> canExecute,
                  INotifyPropertyChanged owner,
                  IEnumerable<string> dependencies);
}
