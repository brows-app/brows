using Brows.Operations;
using Brows.Panels;
using System.Threading.Tasks;

namespace Brows.Commands;

/// <summary>
/// Provides window-level actions for panels and background operations.
/// </summary>
public interface ICommander {
    /// <summary>
    /// Tries to get the commander's background operations.
    /// </summary>
    /// <param name="collection">
    /// Receives the operation collection.
    /// </param>
    /// <returns>
    /// True if the commander has at least one operation; otherwise, false.
    /// </returns>
    bool HasOperations(out IOperationCollection collection);

    /// <summary>
    /// Tries to get the commander's native window.
    /// </summary>
    /// <param name="native">
    /// Receives the platform-specific window.
    /// </param>
    /// <returns>
    /// True if a window is available; otherwise, false.
    /// </returns>
    bool HasWindow(out object native);

    /// <summary>
    /// Adds a panel showing the requested provider.
    /// </summary>
    /// <param name="id">
    /// The provider identifier to open.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the panel was added; otherwise, false.
    /// </returns>
    Task<bool> AddPanel(string id, CancellationToken token);

    /// <summary>
    /// Moves a panel to a column position.
    /// </summary>
    /// <param name="panel">
    /// The panel to move.
    /// </param>
    /// <param name="column">
    /// The destination column position.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the panel was moved; otherwise, false.
    /// </returns>
    Task<bool> ShiftPanel(IPanel panel, int column, CancellationToken token);

    /// <summary>
    /// Removes a panel from the commander.
    /// </summary>
    /// <param name="panel">
    /// The panel to remove.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the panel was removed; otherwise, false.
    /// </returns>
    Task<bool> RemovePanel(IPanel panel, CancellationToken token);

    /// <summary>
    /// Removes the commander panels.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if at least one panel was removed; otherwise, false.
    /// </returns>
    Task<bool> ClearPanels(CancellationToken token);
}
