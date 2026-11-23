using System.Collections.Generic;

namespace Brows.Panels;

/// <summary>
/// Describes files offered to a panel through a drop operation.
/// </summary>
public interface IPanelDrop {
    /// <summary>
    /// Gets the object receiving the drop.
    /// </summary>
    object Target { get; }

    /// <summary>
    /// Gets the file paths offered for copying.
    /// </summary>
    IReadOnlyList<string> CopyFiles { get; }

    /// <summary>
    /// Gets the file paths offered for moving.
    /// </summary>
    IReadOnlyList<string> MoveFiles { get; }
}
