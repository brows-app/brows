using System.Collections.Generic;

namespace Brows.Conf;

/// <summary>
/// Provides configuration loaded from configuration files.
/// </summary>
public sealed class ConfigurationFile : IConfigurationFile {
    private static readonly Dictionary<Type, ConfigurationFileManager> ConfFile = [];

    /// <summary>
    /// Gets the configuration for <typeparamref name="TConfig"/>, loaded from its configuration file.
    /// </summary>
    /// <remarks>
    /// The file is named after <typeparamref name="TConfig"/>, as described for
    /// <see cref="ConfigurationFileManager{TConfig}"/>.
    /// </remarks>
    /// <typeparam name="TConfig">
    /// The type of the configuration.
    /// </typeparam>
    /// <returns>
    /// The configuration shared by every caller for <typeparamref name="TConfig"/>.
    /// </returns>
    public IConfiguration<TConfig> File<TConfig>() where TConfig : class, new() {
        var type = typeof(TConfig);
        lock (ConfFile) {
            if (ConfFile.TryGetValue(type, out var conf) == false) {
                ConfFile[type] = conf = new ConfigurationFileManager<TConfig>();
            }
            return (IConfiguration<TConfig>)conf;
        }
    }
}
