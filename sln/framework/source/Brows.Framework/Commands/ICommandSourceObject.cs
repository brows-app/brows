using System.Collections;

namespace Brows.Commands;

/// <summary>
/// Exposes an object and its related collection as a command source.
/// </summary>
public interface ICommandSourceObject {
    /// <summary>
    /// Gets the current object offered to commands.
    /// </summary>
    public object Instance { get; }

    /// <summary>
    /// Gets the collection of objects offered to commands.
    /// </summary>
    public IEnumerable Collection { get; }
}
