using Domore.Conf;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Conf.ConfigurationFileInfos;

/// <summary>
/// A configuration file whose content is applied by the caller.
/// </summary>
public sealed class NamedConfigurationFileInfo
: ConfigurationFileInfo {
    internal NamedConfigurationFileInfo(string file, string @default, string special)
    : base(file, @default, special) {
    }

    /// <summary>
    /// Calls <paramref name="containerAction"/> with the content of the default file, then the user's file.
    /// </summary>
    /// <param name="containerAction">
    /// The action that applies the content of each file.
    /// </param>
    /// <param name="token">
    /// A token that cancels the operation.
    /// </param>
    /// <returns>
    /// A task that completes when both files are applied.
    /// </returns>
    /// <remarks>
    /// A file that does not exist adds no settings. A file that cannot be read after several
    /// attempts is skipped, and the failure is logged.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="containerAction"/> is null.
    /// </exception>
    public Task Configure(Action<IConfContainer> containerAction, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(containerAction);
        return Configure(
            start: () => true,
            containerAction: (target, conf) => {
                containerAction(conf);
                return target;
            },
            name: Path.GetFileName(File),
            token: token);
    }
}
