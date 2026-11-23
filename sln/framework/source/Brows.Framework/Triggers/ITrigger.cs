namespace Brows.Triggers;

/// <summary>
/// Identifies a configured command trigger.
/// </summary>
public interface ITrigger {
    /// <summary>
    /// Gets the command input to run for this trigger, or null when no replacement input is configured.
    /// </summary>
    string Defined { get; }
}
