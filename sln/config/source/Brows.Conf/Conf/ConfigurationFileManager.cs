using Domore.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Conf;

/// <summary>
/// Manages a configuration loaded from a configuration file in <see cref="ConfigurationPath.FileRoot"/>.
/// </summary>
public abstract class ConfigurationFileManager {
    private protected ConfigurationFileManager() {
    }

    private protected void OnChanged() {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Occurs when the user's configuration file changes. The loaded configuration is discarded, and
    /// the next load reads the files again.
    /// </summary>
    public event EventHandler Changed;
}

/// <summary>
/// Manages a <typeparamref name="TConfig"/> loaded from its configuration file.
/// </summary>
/// <remarks>
/// The file is named after <typeparamref name="TConfig"/> in lower case, without a <c>Config</c>
/// suffix: <c>&lt;name&gt;.conf</c>, applied after the default file
/// <c>default\&lt;name&gt;.conf.default</c>.
/// </remarks>
/// <typeparam name="TConfig">
/// The type of the configuration.
/// </typeparam>
public sealed class ConfigurationFileManager<TConfig> : ConfigurationFileManager, IConfiguration<TConfig>
where TConfig : class, new() {
    /*
     * Log under the base type, so every TConfig shares one log name.
     */
    private static readonly ILog Log = Logging.For(typeof(ConfigurationFileManager));

    private readonly Lock LoadLocker = new();
    private volatile TConfig LoadedValue;
    private volatile int Generation;
    private Task<TConfig> LoadTask;

    private void Invalidated(object sender, EventArgs e) {
        if (Log.Info()) {
            Log.Info($"invalidated > {typeof(TConfig).Name}");
        }
        lock (LoadLocker) {
            LoadedValue = default;
            LoadTask = null;
            Generation++;
        }
        OnChanged();
    }

    private async Task<TConfig> NewLoad() {
        var info = await ConfigurationFileInfo
            .Load<TConfig>(token: CancellationToken.None, invalidated: Invalidated);
        return await info
            .Configure(CancellationToken.None);
    }

    private async Task<TConfig> Ready() {
        var loaded = LoadedValue;
        if (loaded is not null) {
            return loaded;
        }
        var task = default(Task<TConfig>);
        var generation = 0;
        lock (LoadLocker) {
            loaded = LoadedValue;
            if (loaded is not null) {
                return loaded;
            }
            generation = Generation;
            task = LoadTask ??= NewLoad();
        }
        try {
            loaded = await task.ConfigureAwait(false);
            lock (LoadLocker) {
                var loadTaskIsCurrent = LoadTask == task;
                var generationIsCurrent = Generation == generation;
                if (loadTaskIsCurrent) {
                    LoadTask = null;
                }
                if (loadTaskIsCurrent && generationIsCurrent) {
                    LoadedValue ??= loaded;
                }
            }
            return loaded;
        }
        catch {
            lock (LoadLocker) {
                if (LoadTask == task) {
                    LoadTask = null;
                }
            }
            throw;
        }
    }

    /// <summary>
    /// Gets the loaded configuration, or null if it is not loaded or the file changed since it was
    /// loaded.
    /// </summary>
    public TConfig Loaded =>
        LoadedValue;

    /// <summary>
    /// Loads the configuration, or gets it if it is already loaded.
    /// </summary>
    /// <param name="token">
    /// A token that cancels the operation.
    /// </param>
    /// <returns>
    /// The loaded configuration.
    /// </returns>
    public Task<TConfig> Load(CancellationToken token) {
        if (Log.Info()) {
            Log.Info($"{nameof(Load)} > {typeof(TConfig).Name}");
        }
        token.ThrowIfCancellationRequested();
        return Ready().WaitAsync(token);
    }
}
