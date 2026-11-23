using System.Collections.Generic;

namespace Brows.Triggers;

/// <summary>
/// Provides a command's input triggers and its primary trigger.
/// </summary>
public interface IInputTriggerCollection : IReadOnlyCollection<IInputTrigger> {
    /// <summary>
    /// Gets the combined primary trigger, or null when none is configured.
    /// </summary>
    IInputTrigger Main { get; }
}
