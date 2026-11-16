using Brows.Composition;

namespace Brows.Conf;

/// <summary>
/// Provides configuration loaded from configuration files.
/// </summary>
public interface IConfigurationFile : IExport {
    /// <summary>
    /// Gets the configuration for <typeparamref name="TConfig"/>, loaded from its configuration file.
    /// </summary>
    /// <typeparam name="TConfig">
    /// The type of the configuration.
    /// </typeparam>
    /// <returns>
    /// The configuration shared by every caller for <typeparamref name="TConfig"/>.
    /// </returns>
    IConfiguration<TConfig> File<TConfig>() where TConfig : class, new();
}
