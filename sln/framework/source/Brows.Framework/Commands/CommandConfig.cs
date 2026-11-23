namespace Brows.Commands;

/// <summary>
/// Controls whether a command is enabled.
/// </summary>
public class CommandConfig {
    /// <summary>
    /// Gets or sets whether the command is enabled. The default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
