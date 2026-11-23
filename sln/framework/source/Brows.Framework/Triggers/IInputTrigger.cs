using System.Collections.Generic;

namespace Brows.Triggers;

/// <summary>
/// Matches command input against a primary name and its aliases.
/// </summary>
public interface IInputTrigger : ITrigger {
    /// <summary>
    /// Gets the primary input that activates the trigger.
    /// </summary>
    string Input { get; }

    /// <summary>
    /// Gets alternative inputs that activate the trigger.
    /// </summary>
    IReadOnlySet<string> Aliases { get; }

    /// <summary>
    /// Gets all accepted inputs, including the primary input and its aliases.
    /// </summary>
    IReadOnlySet<string> Options { get; }

    /// <summary>
    /// Determines whether text matches an accepted input.
    /// </summary>
    /// <param name="s">
    /// The input to test.
    /// </param>
    /// <returns>
    /// True if the input matches; otherwise, false.
    /// </returns>
    bool Triggered(string s);
}
