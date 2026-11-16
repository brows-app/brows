using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Conf;

/// <summary>
/// Manages configuration data stored by the application.
/// </summary>
public abstract class ConfigurationDataManager : IConfiguration {
    private protected ConfigurationDataManager() {
    }

    event EventHandler IConfiguration.Changed {
        add { }
        remove { }
    }
}

/// <summary>
/// Manages configuration data of type <typeparamref name="TData"/>, stored in
/// <c>&lt;name&gt;.brws</c> in <see cref="ConfigurationPath.DataRoot"/>.
/// </summary>
/// <remarks>
/// When a property of the loaded data changes, the data is saved about a second later.
/// </remarks>
/// <typeparam name="TData">
/// The type of the configuration data.
/// </typeparam>
public sealed class ConfigurationDataManager<TData> : ConfigurationDataManager, IConfiguration<TData>
where TData : class, INotifyPropertyChanged, new() {
    private int ChangeState;
    private readonly int ChangeDelay = 1000;
    private readonly SemaphoreSlim ChangeLocker = new(1, 1);
    private readonly Lock LoadLocker = new();
    private volatile TData LoadedValue;
    private Task<TData> LoadTask;

    private ConfigurationDataPayload<TData> Payload(TData data) {
        return ConfigurationDataPayload.For(data, Name);
    }

    private async void Loaded_PropertyChanged(object sender, EventArgs e) {
        var changeState = ChangeState = ChangeState + 1; await Task.Delay(ChangeDelay);
        if (changeState == ChangeState) {
            await ChangeLocker.WaitAsync();
            try {
                if (changeState == ChangeState) {
                    ChangeState = 0;
                    var payload = Payload(Loaded);
                    await payload.Save(CancellationToken.None);
                }
            }
            finally {
                ChangeLocker.Release();
            }
        }
    }

    private async Task<TData> NewLoad() {
        var
        loaded = await Payload(new TData()).Load(CancellationToken.None);
        loaded.PropertyChanged += Loaded_PropertyChanged;
        return loaded;
    }

    private async Task<TData> Ready() {
        var loaded = LoadedValue;
        if (loaded is not null) {
            return loaded;
        }
        var task = default(Task<TData>);
        lock (LoadLocker) {
            loaded = LoadedValue;
            if (loaded is not null) {
                return loaded;
            }
            task = LoadTask ??= NewLoad();
        }
        try {
            loaded = await task.ConfigureAwait(false);
            lock (LoadLocker) {
                LoadedValue ??= loaded;
                LoadTask = null;
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

    internal string Name { get; }

    /// <summary>
    /// Gets the loaded configuration data, or null if it is not loaded.
    /// </summary>
    public TData Loaded =>
        LoadedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationDataManager{TData}"/> class.
    /// </summary>
    /// <param name="name">
    /// The name of the data file, without its extension.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty, white space, or not a valid file name.
    /// </exception>
    public ConfigurationDataManager(string name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var nameIsInvalid = name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
        if (nameIsInvalid) {
            throw new ArgumentException($"The name '{name}' is not a valid file name.", nameof(name));
        }
        Name = name;
    }

    /// <summary>
    /// Loads the configuration data, or gets it if it is already loaded. If the file does not
    /// exist, the data is a new <typeparamref name="TData"/>.
    /// </summary>
    /// <param name="token">
    /// A token that cancels the operation.
    /// </param>
    /// <returns>
    /// The loaded configuration data.
    /// </returns>
    public Task<TData> Load(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        return Ready().WaitAsync(token);
    }
}
