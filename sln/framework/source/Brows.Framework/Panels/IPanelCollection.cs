using System.Collections.Generic;

namespace Brows.Panels;

/// <summary>
/// Provides the panels, active panel, passive panel, and location history.
/// </summary>
public interface IPanelCollection {
    /// <summary>
    /// Gets the panel at a zero-based position.
    /// </summary>
    /// <param name="index">
    /// The zero-based panel position.
    /// </param>
    /// <returns>
    /// The panel at that position.
    /// </returns>
    IPanel this[int index] { get; }

    /// <summary>
    /// Gets the number of panels.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the active panel, or null when none is active.
    /// </summary>
    IPanel Active { get; }

    /// <summary>
    /// Gets the passive panel, or null when none is available.
    /// </summary>
    IPanel Passive { get; }

    /// <summary>
    /// Gets previously visited provider identifiers.
    /// </summary>
    IReadOnlyCollection<string> History { get; }

    /// <summary>
    /// Tries to get a panel by its column position.
    /// </summary>
    /// <param name="column">
    /// The zero-based column position.
    /// </param>
    /// <param name="panel">
    /// Receives the panel when found.
    /// </param>
    /// <returns>
    /// True if the column contains a panel; otherwise, false.
    /// </returns>
    bool HasColumn(int column, out IPanel panel);

    /// <summary>
    /// Enumerates the panels in column order.
    /// </summary>
    /// <returns>
    /// An enumerator over the panels.
    /// </returns>
    IEnumerator<IPanel> GetEnumerator();

    /// <summary>
    /// Gets the panels in column order.
    /// </summary>
    /// <returns>
    /// The panels.
    /// </returns>
    IEnumerable<IPanel> AsEnumerable();
}
