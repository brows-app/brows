using System.Collections.Generic;
using System.ComponentModel;

namespace Brows.Conf;

/// <summary>
/// Provides configuration data stored by the application.
/// </summary>
public sealed class ConfigurationData : IConfigurationData {
    private static readonly Dictionary<string, ConfigurationDataManager> DataFile =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the configuration data stored in <c>&lt;name&gt;.brws</c> in <see cref="ConfigurationPath.DataRoot"/>.
    /// </summary>
    /// <typeparam name="TConfig">
    /// The type of the configuration data.
    /// </typeparam>
    /// <param name="name">
    /// The name of the data file, without its extension.
    /// </param>
    /// <returns>
    /// The configuration data shared by every caller for <paramref name="name"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty, white space, or not a valid file name.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="name"/> is already used for configuration data of a different type.
    /// </exception>
    public IConfiguration<TConfig> Data<TConfig>(string name) where TConfig : class, INotifyPropertyChanged, new() {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (DataFile) {
            if (DataFile.TryGetValue(name, out var data) == false) {
                DataFile[name] = data = new ConfigurationDataManager<TConfig>(name);
            }
            if (data is IConfiguration<TConfig> config) {
                return config;
            }
            throw new InvalidOperationException(
                $"The configuration data '{name}' is already used for a different type than {typeof(TConfig).Name}.");
        }
    }
}
