using Brows.Composition;

namespace Brows.Config;

/// <summary>
/// Configuration variables for the config framework.
/// </summary>
public sealed record ConfigExportVariable : IExportVariable {
    /// <summary>
    /// Gets or sets the default config kind to use.
    /// </summary>
    public ConfigKind? DefaultConfig { get; init; }

    /// <summary>
    /// Gets or sets the user config variables.
    /// </summary>
    public ConfigKindVariable UserConfig { get; init; }

    /// <summary>
    /// Gets or sets the common config variables.
    /// </summary>
    public ConfigKindVariable CommonConfig { get; init; }
}
