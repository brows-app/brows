using System.Collections;

namespace Brows.Commands;

/// <summary>
/// Provides the current source item and the collection on which a command can act.
/// </summary>
public interface ICommandSource {
    /// <summary>
    /// Gets the current source item.
    /// </summary>
    public object Item { get; }

    /// <summary>
    /// Gets the collection of source items.
    /// </summary>
    public IEnumerable Items { get; }
}
