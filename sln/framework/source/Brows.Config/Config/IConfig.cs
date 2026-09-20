using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config;

/// <summary>
/// Reads and writes data and configuration objects.
/// </summary>
public interface IConfig {
    /// <summary>
    /// Gets the root path on the file system of this config.
    /// </summary>
    string Root { get; }

    /// <summary>
    /// Gets the kind of configuration.
    /// </summary>
    ConfigKind Kind { get; }

    /// <summary>
    /// Reads an instance of <typeparamref name="T"/> from data storage.
    /// </summary>
    /// <typeparam name="T">The type of data to read.</typeparam>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>An instance of <typeparamref name="T"/> that has been read from data storage.</returns>
    Task<T> Read<T>(CancellationToken token) where T : new();

    /// <summary>
    /// Reads data for an instance of <typeparamref name="T"/> from data storage.
    /// </summary>
    /// <typeparam name="T">The type of data to read.</typeparam>
    /// <param name="target">The instance that receives the data.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>The instance of <typeparamref name="T"/> that has been read from data storage.</returns>
    Task<T> Read<T>(T target, CancellationToken token);

    /// <summary>
    /// Persists the <paramref name="target"/> to data storage.
    /// </summary>
    /// <typeparam name="T">The type of the <paramref name="target"/>.</typeparam>
    /// <param name="target">The object persisted to data storage.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>
    /// A task that signals the completion of the write and results in the value
    /// of the passed <paramref name="target"/> parameter.
    /// </returns>
    Task<T> Write<T>(T target, CancellationToken token);

    /// <summary>
    /// Updates data for objects of type <typeparamref name="T"/> in storage.
    /// </summary>
    /// <typeparam name="T">The type of the object whose data is updated.</typeparam>
    /// <param name="mutate">Callback invoked to modify the object's state.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that completes when the object's data has been updated in storage.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="mutate"/> is null.</exception>
    Task<T> Update<T>(Action<T> mutate, CancellationToken token) where T : new();

    /// <summary>
    /// Watches for configuration changes of the type <typeparamref name="T"/> in storage.
    /// </summary>
    /// <typeparam name="T">The type of the object whose data is watched.</typeparam>
    /// <param name="configured">Called when the configuration object is configured.</param>
    /// <param name="errored">Called when a configuration error occurs.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>The instance of <typeparamref name="T"/> that is being watched in data storage.</returns>
    Task<T> Watch<T>(Action configured, Action<Exception> errored, CancellationToken token) where T : new();
}
