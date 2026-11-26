using System.Threading.Tasks;

namespace Brows.Conf.ConfigurationFileInfos;

/// <summary>
/// A configuration file that configures a new <typeparamref name="TConfig"/>.
/// </summary>
/// <typeparam name="TConfig">
/// The type of the configuration.
/// </typeparam>
public sealed class TypedConfigurationFileInfo<TConfig>
: ConfigurationFileInfo where TConfig : new() {
    internal TypedConfigurationFileInfo(string file, string @default)
    : base(file, @default) {
    }

    /// <summary>
    /// Creates a <typeparamref name="TConfig"/> configured from the default file, then the user's file.
    /// </summary>
    /// <param name="token">
    /// A token that cancels the operation.
    /// </param>
    /// <returns>
    /// The configured <typeparamref name="TConfig"/>.
    /// </returns>
    /// <remarks>
    /// A file that does not exist adds no settings. A file that cannot be read after several
    /// attempts is skipped, and the failure is logged.
    /// </remarks>
    public Task<TConfig> Configure(CancellationToken token) {
        return Configure(
            start: () => new TConfig(),
            containerAction: (target, conf) => conf.Configure(target, key: ""),
            name: typeof(TConfig).Name,
            token: token);
    }
}
