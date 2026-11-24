using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Conf;

/// <summary>
/// Provides the directories that hold configuration files and configuration data.
/// </summary>
public static class ConfigurationPath {
    private static string DataRootDefault =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify),
            "Brows",
            "data");

    private static Task<string> Ready(string directory, CancellationToken cancellationToken) {
        return Task.Run(cancellationToken: cancellationToken, function: async () => {
            if (Directory.Exists(directory) == false) {
                Directory.CreateDirectory(directory);
                for (; ; ) {
                    if (Directory.Exists(directory)) {
                        break;
                    }
                    await Task.Delay(1, cancellationToken);
                }
            }
            return directory;
        });
    }

    internal const string DataRootVariable = "BROWS_DATA_ROOT";

    internal static string DataRootFrom(string value) {
        var path = value?.Trim();
        var pathIsQualified = path is not null && Path.IsPathFullyQualified(path);
        if (pathIsQualified) {
            return Path.GetFullPath(path);
        }
        return DataRootDefault;
    }

    /// <summary>
    /// Creates the <see cref="DataRoot"/> directory if it does not exist.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that cancels the operation.
    /// </param>
    /// <returns>
    /// The path of the <see cref="DataRoot"/> directory, once it exists.
    /// </returns>
    public static Task<string> DataReady(CancellationToken cancellationToken) {
        return Ready(DataRoot, cancellationToken);
    }

    /// <summary>
    /// Creates the <see cref="FileRoot"/> directory if it does not exist.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that cancels the operation.
    /// </param>
    /// <returns>
    /// The path of the <see cref="FileRoot"/> directory, once it exists.
    /// </returns>
    public static Task<string> FileReady(CancellationToken cancellationToken) {
        return Ready(FileRoot, cancellationToken);
    }

    /// <summary>
    /// Gets the path of the directory that holds configuration data saved by the application,
    /// <c>Brows\data</c> in the local application data folder. When the <c>BROWS_DATA_ROOT</c>
    /// environment variable holds a fully qualified path, that path is used instead.
    /// </summary>
    public static string DataRoot =>
        field ??= DataRootFrom(Environment.GetEnvironmentVariable(DataRootVariable));

    /// <summary>
    /// Gets the path of the directory that holds configuration files, <c>brows.config</c> in the
    /// directory of the running executable. Default configuration files are in its <c>default</c>
    /// subdirectory.
    /// </summary>
    public static string FileRoot =>
        field ??= Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath),
            "brows.config");
}
