namespace Brows.Commands;

/// <summary>
/// Identifies the command associated with a palette hint.
/// </summary>
public interface ICommandContextHint {
    /// <summary>
    /// Gets the command associated with the hint.
    /// </summary>
    ICommand Command { get; }
}
