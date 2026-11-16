using System;
using System.Threading.Tasks;

namespace Brows.Conf;

/// <summary>
/// A configuration that can change after it is loaded.
/// </summary>
public interface IConfiguration {
    /// <summary>
    /// Occurs when the stored configuration changes, such as when its file is edited. After it
    /// occurs, loading the configuration gets the changed values. Not every implementation
    /// raises it.
    /// </summary>
    event EventHandler Changed;
}

/// <summary>
/// A configuration of type <typeparamref name="TConfig"/> that is loaded on demand.
/// </summary>
/// <typeparam name="TConfig">
/// The type of the configuration.
/// </typeparam>
public interface IConfiguration<TConfig> : IConfiguration {
    /// <summary>
    /// Gets the loaded configuration, or null if it is not loaded.
    /// </summary>
    TConfig Loaded { get; }

    /// <summary>
    /// Loads the configuration if it is not loaded, and gets it.
    /// </summary>
    /// <param name="token">
    /// A token that cancels the operation.
    /// </param>
    /// <returns>
    /// The loaded configuration.
    /// </returns>
    Task<TConfig> Load(CancellationToken token);
}
