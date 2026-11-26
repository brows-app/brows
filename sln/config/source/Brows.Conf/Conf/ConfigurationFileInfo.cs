using Brows.Conf.ConfigurationFileInfos;
using Domore.Conf;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CONF = Domore.Conf.Conf;

namespace Brows.Conf;

/// <summary>
/// Describes a configuration file and the default file applied before it.
/// </summary>
public abstract class ConfigurationFileInfo {
    private static readonly ILog Log = Logging.For(typeof(ConfigurationFileInfo));
    private static readonly Dictionary<string, object> Locker = new(StringComparer.OrdinalIgnoreCase);

    private event EventHandler Invalidated;

    private protected ConfigurationFileInfo(string file, string @default) {
        File = file;
        Default = @default;
    }

    private static async Task<(string File, string Default)> Paths(string name, CancellationToken token) {
        var dir = await ConfigurationPath.FileReady(token);
        var file = Path.Combine(dir, $"{name}.conf");
        var dflt = Path.Combine(dir, "default", $"{name}.conf.default");
        return (file, dflt);
    }

    private protected Task<TResult>
    Configure<TResult>(Func<TResult> start,
                       Func<TResult, IConfContainer, TResult> containerAction,
                       string name,
                       CancellationToken token) {
        var file = File;
        var fileLock = default(object);
        lock (Locker) {
            if (Locker.TryGetValue(file, out fileLock) == false) {
                Locker[file] = fileLock = new();
            }
        }
        return Task.Run(cancellationToken: token, function: () => {
            lock (fileLock) {
                TResult config(TResult target, string file) {
                    for (var i = 0; ; i++) {
                        var err = default(Exception);
                        var conf = CONF.Contain(file, special: "@conf", comment: "#");
                        try {
                            return containerAction(target, conf);
                        }
                        catch (IOException ex) {
                            err = ex;
                        }
                        if (i > 3) {
                            if (Log.Error()) {
                                Log.Error(err);
                            }
                            if (Log.Warn()) {
                                Log.Warn($"Unable to configure > {name}");
                            }
                            return target;
                        }
                        if (Log.Info()) {
                            Log.Info(err?.Message);
                        }
                        Thread.Sleep(250);
                    }
                }
                var
                target = start();
                target = config(target, Default);
                target = config(target, file);
                return target;
            }
        });
    }

    internal bool Invalid { get; private set; }
    internal string File { get; }
    internal string Default { get; }

    internal void Invalidate() {
        if (Log.Info()) {
            Log.Info(nameof(Invalidate) + " > " + File);
        }
        Invalid = true;
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    internal static async Task<TypedConfigurationFileInfo<TConfig>> Load<TConfig>(EventHandler invalidated,
                                                                                  CancellationToken token)
    where TConfig : new() {
        var type = typeof(TConfig).Name.ToLowerInvariant();
        var name = type.EndsWith("config") ? type.Substring(0, type.Length - "config".Length) : type;
        var paths = await Paths(name, token);
        var
        loaded = new TypedConfigurationFileInfo<TConfig>(paths.File, paths.Default);
        loaded.Invalidated += invalidated;
        await ConfigurationFileWatcher.Subscribe(loaded, token);
        return loaded;
    }

    /// <summary>
    /// Gets the configuration file <c>&lt;name&gt;.conf</c> in <see cref="ConfigurationPath.FileRoot"/>, and its
    /// default file <c>default\&lt;name&gt;.conf.default</c>. Neither file is read until it is configured.
    /// </summary>
    /// <param name="name">
    /// The name of the file without its <c>.conf</c> extension.
    /// </param>
    /// <param name="invalidated">
    /// Called once, the first time the user's file changes, or null to not watch the file.
    /// </param>
    /// <param name="token">
    /// A token that cancels the operation.
    /// </param>
    /// <returns>
    /// The configuration file, which applies the default file and then the user's file when it is
    /// configured.
    /// </returns>
    public static async Task<NamedConfigurationFileInfo> Load(string name,
                                                              EventHandler invalidated,
                                                              CancellationToken token) {
        var paths = await Paths(name, token);
        var
        loaded = new NamedConfigurationFileInfo(paths.File, paths.Default);
        if (invalidated is not null) {
            loaded.Invalidated += invalidated;
            await ConfigurationFileWatcher.Subscribe(loaded, token);
        }
        return loaded;
    }
}
