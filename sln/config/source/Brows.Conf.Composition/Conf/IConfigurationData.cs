using Brows.Composition;
using System.ComponentModel;

namespace Brows.Conf;

/// <summary>
/// Provides configuration data stored by the application.
/// </summary>
public interface IConfigurationData : IExport {
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
    IConfiguration<TConfig> Data<TConfig>(string name) where TConfig : class, INotifyPropertyChanged, new();
}
