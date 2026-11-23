namespace Brows.Commands;

/// <summary>
/// Provides browsable command data and actions for navigating or changing it.
/// </summary>
public interface ICommandContextData {
    /// <summary>
    /// Gets configuration text associated with the current item, or null when none is supplied.
    /// </summary>
    string Conf { get; }

    /// <summary>
    /// Gets command input associated with the current item, or null when none is supplied.
    /// </summary>
    string Input { get; }

    /// <summary>
    /// Gets the current data item.
    /// </summary>
    object Current { get; }

    /// <summary>
    /// Gets the command associated with this data.
    /// </summary>
    ICommand Command { get; }

    /// <summary>
    /// Gets the palette input behavior associated with the current item.
    /// </summary>
    ICommandContextFlag Flag { get; }

    /// <summary>
    /// Removes the current data item.
    /// </summary>
    /// <returns>
    /// The resulting data context, or null when no context remains.
    /// </returns>
    ICommandContextData Remove();

    /// <summary>
    /// Clears the command data.
    /// </summary>
    /// <returns>
    /// The resulting data context, or null when no context remains.
    /// </returns>
    ICommandContextData Clear();

    /// <summary>
    /// Moves to the next data item.
    /// </summary>
    /// <returns>
    /// The resulting data context, or null when no context remains.
    /// </returns>
    ICommandContextData Next();

    /// <summary>
    /// Moves to the previous data item.
    /// </summary>
    /// <returns>
    /// The resulting data context, or null when no context remains.
    /// </returns>
    ICommandContextData Previous();

    /// <summary>
    /// Accepts the current data item.
    /// </summary>
    /// <returns>
    /// The resulting data context, or null when no context remains.
    /// </returns>
    ICommandContextData Enter();
}
