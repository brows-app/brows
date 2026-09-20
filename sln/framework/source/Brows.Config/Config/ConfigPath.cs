using Domore.Logs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config;

/// <summary>
/// This class is a base for implementations of <see cref="IConfig"/>.
/// </summary>
internal abstract class ConfigPath : IConfig {
    private static readonly ILog Log = Logging.For(typeof(ConfigPath));

    /// <summary>
    /// Gets the name of the executable, minus its extension, that started
    /// the process.
    /// </summary>
    private protected static string AppName => field ??=
        Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);

    /// <summary>
    /// When overridden in a derived class, gets the path to the directory
    /// in which configuration files are stored for the instance of <see cref="ConfigPath"/>.
    /// </summary>
    protected internal abstract string Root { get; }

    /// <summary>
    /// When overridden in a derived class, gets the config kind.
    /// </summary>
    protected internal abstract ConfigKind Kind { get; }

    /// <summary>
    /// Adjusts the specified path to ensure it is rooted, using the provided default root if necessary.
    /// </summary>
    /// <remarks>This method ensures that the returned path is properly rooted and uses the system's directory
    /// separator character. If the input path is relative, it is combined with <paramref name="defaultPathRoot"/> to
    /// produce an absolute path.</remarks>
    /// <param name="path">The input path to adjust. Can be relative or absolute. If null or empty, the method returns the current root.</param>
    /// <param name="defaultPathRoot">The default root directory to use if <paramref name="path"/> is relative.</param>
    /// <returns>
    /// A rooted path with directory separators normalized. If <paramref name="path"/> is null or empty, the method
    /// returns the current root.
    /// </returns>
    protected string ChangeRoot(string path, string defaultPathRoot) {
        var trimPath = path?.Trim() ?? "";
        if (trimPath == "") {
            /*
             * If no custom path information was given,
             * then don't change anything.
             */
            return Root;
        }
        var fixedPath = trimPath
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);
        var fixedPathHasRoot = Path.IsPathRooted(fixedPath);
        if (fixedPathHasRoot == false) {
            fixedPath = Path.Combine(defaultPathRoot, fixedPath);
        }
        return fixedPath;
    }

    /// <summary>
    /// Returns the string representation of the current object.
    /// </summary>
    /// <returns>The value of the <see cref="Root"/> property.</returns>
    public sealed override string ToString() {
        return Root;
    }

    string IConfig.Root => Root;

    ConfigKind IConfig.Kind => Kind;

    Task<T> IConfig.Write<T>(T target, CancellationToken token) {
        if (null == target) throw new ArgumentNullException(nameof(target));
        var type = target.GetType();
        if (Log.Info()) {
            Log.Info($"Writing: {type}");
        }
        var file = ConfigFile.For(type);
        return file.Lock(token: token, task: (info, token) => {
            return info.Write(Root, target, token);
        });
    }

    Task<T> IConfig.Read<T>(T target, CancellationToken token) {
        if (null == target) throw new ArgumentNullException(nameof(target));
        var type = target.GetType();
        if (Log.Info()) {
            Log.Info($"Reading: {type}");
        }
        var file = ConfigFile.For(type);
        return file.Lock(token: token, task: (info, token) => {
            return info.Read(Root, target, token);
        });
    }

    Task<T> IConfig.Read<T>(CancellationToken token) {
        if (Log.Info()) {
            Log.Info($"Reading: {typeof(T)}");
        }
        var file = ConfigFile.For(typeof(T));
        return file.Lock(token: token, task: (info, token) => {
            return info.Read<T>(Root, token);
        });
    }

    Task<T> IConfig.Update<T>(Action<T> mutate, CancellationToken token) {
        if (null == mutate) throw new ArgumentNullException(nameof(mutate));
        if (Log.Info()) {
            Log.Info($"Updating: {typeof(T)}");
        }
        var file = ConfigFile.For(typeof(T));
        return file.Lock(token: token, task: async (info, token) => {
            var target = await info.Read<T>(Root, token).ConfigureAwait(false);
            mutate(target);
            return await info.Write(Root, target, token).ConfigureAwait(false);
        });
    }

    Task<T> IConfig.Watch<T>(Action configured, Action<Exception> errored, CancellationToken token) {
        var file = ConfigFile.For(typeof(T));
        return file.Lock(token: token, task: (info, token) => {
            return info.Watch<T>(Root, configured, errored, token);
        });
    }
}
