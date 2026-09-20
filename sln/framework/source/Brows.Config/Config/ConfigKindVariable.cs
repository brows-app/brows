namespace Brows.Config;

/// <summary>
/// Variables for config providers.
/// </summary>
public sealed record ConfigKindVariable {
    /// <summary>
    /// Gets or sets the path for the config provider.
    /// </summary>
    public string Path { get; init; }
}
