using System;

namespace Brows.Gui;

/// <summary>
/// Reports when a view is loaded.
/// </summary>
public interface IController {
    /// <summary>
    /// Occurs when the view is loaded.
    /// </summary>
    event EventHandler Loaded;
}
