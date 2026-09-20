using Brows.Config.FileSerializers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config;

/// <summary>
/// Base class for types that serialize configuration and data to and from strings.
/// </summary>
internal abstract class ConfigFileSerializer {
    private static readonly ConfigFileSerializer Conf = new ConfSerializer();
    private static readonly ConfigFileSerializer Data = new DataSerializer();

    /// <summary>
    /// When overridden in a derived class, gets the file extension of the serializer.
    /// </summary>
    public abstract string Extension { get; }

    /// <summary>
    /// When overridden in a derived class, serializes the object <paramref name="target"/>
    /// to text.
    /// </summary>
    /// <param name="target">The object to be serialized.</param>
    /// <returns>The serialized text of the object.</returns>
    public abstract string Serialize(object target);

    /// <summary>
    /// When overridden in a derived class, deserializes the string <paramref name="text"/>
    /// to an instance of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of instance to be returned.</typeparam>
    /// <param name="text">The text to be deserialized.</param>
    /// <returns>The instance of <typeparamref name="T"/> into which <paramref name="text"/> has been deserialized.</returns>
    public abstract T Deserialize<T>(string text) where T : new();

    /// <summary>
    /// When overridden in a derived class, deserializes the string <paramref name="text"/>
    /// to the target object.
    /// </summary>
    /// <param name="target">The object to receive population.</param>
    /// <param name="text">The text to be deserialized.</param>
    public abstract void Populate(object target, string text);

    /// <summary>
    /// When overridden in a derived class, watches for configuration changes.
    /// </summary>
    /// <param name="path">The path to watch.</param>
    /// <param name="target">The object to receive population.</param>
    /// <param name="configured">Called when the configuration object is configured.</param>
    /// <param name="errored">Called when a configuration error occurs.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>An instance of <see cref="IDisposable"/> that, when disposed, stops watching.</returns>
    public abstract Task<IDisposable> Watch(string path, object target, Action configured, Action<Exception> errored, CancellationToken token);

    /// <summary>
    /// Gets the appropriate <see cref="ConfigFileSerializer"/> for the specified <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind">The kind of <see cref="ConfigFileKind"/> for which an instance of <see cref="ConfigFileSerializer"/> is returned.</param>
    /// <returns>The instance of <see cref="ConfigFileSerializer"/> for the specified <paramref name="kind"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the value of <paramref name="kind"/> is invalid.</exception>
    public static ConfigFileSerializer For(ConfigFileKind kind) {
        switch (kind) {
            case ConfigFileKind.Conf:
                return Conf;
            case ConfigFileKind.Json:
            case ConfigFileKind.Data:
                return Data;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(kind), message: $"Invalid config path kind '{kind}'");
        }
    }
}
