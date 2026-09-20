using Brows.IPC.Client;
using Brows.IPC.Client.ClientStreamProviders;
using Brows.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC;

/// <summary>
/// Instances of this type are responsible for serializing and deserializing objects to and from JSON text.
/// </summary>
public sealed class ObjectClient : IDisposable {
    private readonly SemaphoreSlim Locker = new(1, 1);

    private JsonSerializer SerializerCache;
    private JsonSerializerSettings SerializerSettingsCache;

    private JsonSerializerSettings SerializerSettings() {
        if (SerializerSettingsCache is not null) {
            return SerializerSettingsCache;
        }
        void callback(JsonSerializerSettings settings) {
            /*
             * Using no formatting is important because it ensures JSON strings exist on a single line,
             * i.e. no new-line characters exist in the string. New-line characters are used to delimit
             * messages, so if they were to exist in JSON strings, we'd have issues.
             * 
             * We also remove any string-enum converters, because we're interested in saving bits, here.
             */
            settings.Formatting = Formatting.None;
            settings.Converters = settings.Converters switch {
                var items when items is not null => [.. items.Where(item => item is not StringEnumConverter)],
                _ => null
            };
        }
        var factory = Imports.Current.Find<IJsonSerializerSettingsFactory>(throwIfNotFound: false, throwIfNotReady: false);
        if (factory is not null) {
            return SerializerSettingsCache = factory.Create(callback);
        }
        /*
         * These settings aren't cached as we wait for the imports to be ready.
         */
        factory = JsonSerializerSettingsFactory.Default;
        return factory.Create(callback);
    }

    private JsonSerializer Serializer() {
        if (SerializerCache is not null) {
            return SerializerCache;
        }
        var settings = SerializerSettings();
        /*
         * Reference equality: if the settings came from the cached DI path,
         * we can cache the serializer too. If they came from the temporary
         * fallback path (imports not ready), we create a throwaway serializer.
         */
        if (settings == SerializerSettingsCache) {
            return SerializerCache = JsonSerializer.Create(settings);
        }
        return JsonSerializer.Create(settings);
    }

    private bool Locked;

    private ObjectTypeResolver TypeResolver { get; }
    private ClientStreamProvider StreamProvider { get; }

    private ObjectClient(ClientStreamProvider streamProvider, IObjectTypeResolver typeResolver) {
        StreamProvider = streamProvider ?? throw new ArgumentNullException(nameof(streamProvider));
        TypeResolver = new(typeResolver ?? throw new ArgumentNullException(nameof(typeResolver)));
    }

    private void Dispose(bool disposing) {
        if (disposing) {
            using (Locker) {
                StreamProvider.Dispose();
            }
        }
    }

    private string Serialize(ObjectInstanceWrapper wrapper) {
        var serializer = Serializer();
        var obj = new ApiInstanceWrapper {
            ApiVersion = 1,
            Object = wrapper
        };
        var sb = new StringBuilder(256);
        using (var sw = new StringWriter(sb, CultureInfo.InvariantCulture)) {
            using (JsonTextWriter jsonWriter = new(sw)) {
                jsonWriter.Formatting = Formatting.None;
                serializer.Serialize(jsonWriter, obj, typeof(ApiInstanceWrapper));
            }
            return sw.ToString();
        }
    }

    private ObjectInstanceWrapper Unwrap(string json) {
        /*
         * Deserialization is two-phase: first deserialize into ApiJsonWrapper,
         * which preserves the object payload as a raw JToken. Then read the
         * type metadata from the envelope, resolve it to a CLR type, and
         * finally deserialize the JToken into the resolved type. This is
         * necessary because we don't know the target type until we read
         * the metadata.
         */
        var serializer = Serializer();
        var apiJsonWrapper = default(ApiJsonWrapper);
        using (var textReader = new StringReader(json)) {
            using (var jsonReader = new JsonTextReader(textReader)) {
                apiJsonWrapper = serializer.Deserialize<ApiJsonWrapper>(jsonReader);
            }
        }
        var objJsonWrapper = apiJsonWrapper.Object ?? throw new ObjectApiException("No object");
        var objTypeInfo = objJsonWrapper.ObjectTypeInfo();
        var objType = TypeResolver.Resolve(objTypeInfo, throwOnNotResolved: true);
        var obj = objJsonWrapper.ObjectToken switch {
            var objectToken when objectToken is not null =>
                objectToken.ToObject(objType, serializer),
            _ => null
        };
        return new() {
            ObjectToken = obj,
            ObjectSource = objTypeInfo?.TypeSource,
            ObjectType = objTypeInfo?.TypeName,
            ObjectVersion = objTypeInfo?.TypeVersion
        };
    }

    private ObjectInstanceWrapper Wrap(object obj) {
        if (null == obj) throw new ArgumentNullException(nameof(obj));
        var type = obj.GetType();
        var info = TypeResolver.Reverse(type, throwOnNotFound: true);
        return new() {
            ObjectType = info?.TypeName,
            ObjectSource = info?.TypeSource,
            ObjectVersion = info?.TypeVersion,
            ObjectToken = obj
        };
    }

    private Task<object> WriteString(string value, CancellationToken token) {
        var provider = StreamProvider;
        try {
            return provider.Use(token: token, function: async (stream, token) => {
                try {
                    await stream.Write(value, token);
                    return default(object);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) {
                    /*
                     * Don't wrap these exceptions.
                     * Cancellations must be thrown as-is.
                     */
                    throw;
                }
                catch (Exception ex) {
                    /*
                     * All other exceptions are wrapped in a well-known exception,
                     * so they can be easily identified.
                     */
                    throw new ClientStreamException(provider, ex);
                }
            });
        }
        catch (ObjectDisposedException ex) {
            /*
             * We need to investigate further if this is really necessary,
             * or if it should be handled differently.
             */
            throw new ClientStreamException(provider, ex);
        }
    }

    private Task<string> ReadString(CancellationToken token) {
        var provider = StreamProvider;
        try {
            return provider.Use(token: token, function: async (stream, token) => {
                try {
                    return await stream.Read(token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) {
                    /*
                     * Don't wrap these exceptions.
                     * Cancellations must be thrown as-is.
                     */
                    throw;
                }
                catch (Exception ex) {
                    /*
                     * All other exceptions are wrapped in a well-known exception,
                     * so they can be easily identified.
                     */
                    throw new ClientStreamException(provider, ex);
                }
            });
        }
        catch (ObjectDisposedException ex) {
            /*
             * We need to investigate further if this is really necessary,
             * or if it should be handled differently.
             */
            throw new ClientStreamException(provider, ex);
        }
    }

    private async Task<T> Lock<T>(Func<CancellationToken, Task<T>> function, CancellationToken token) {
        if (function is null) {
            throw new ArgumentNullException(nameof(function));
        }
        /*
         * Protect against reentrancy and possible dead locks.
         */
        if (Locked) {
            throw new InvalidOperationException("Already locked.");
        }
        await
        Locker.WaitAsync(token);
        Locked = true;
        try {
            var task = function(token);
            if (task is not null) {
                return await task;
            }
            return default;
        }
        finally {
            try {
                Locker.Release();
            }
            catch (ObjectDisposedException) {
                /*
                 * The semaphore may be disposed (which means this object
                 * was disposed) during the task, so don't throw here. If
                 * this method is called again after disposal, the above
                 * call to wait will throw a similar exception, which will
                 * notify the caller that something went wrong.
                 */
            }
            Locked = false;
        }
    }

    private Task<string> Write(bool locked, object obj, Func<string, CancellationToken, Task> onSerialized, CancellationToken token) {
        /*
         * The 'locked' parameter controls whether this operation acquires
         * the semaphore. The public Add/Read methods always lock, but
         * the Call method acquires the lock at a higher level and then
         * calls Add and Read with locked=false to avoid deadlocking
         * on the non-reentrant semaphore.
         */
        async Task<string> core(CancellationToken token) {
            var s = MakeString(obj);
            var cb = onSerialized?.Invoke(s, token);
            if (cb is not null) {
                await cb;
            }
            await WriteString(s, token);
            return s;
        }
        return locked == false
            ? core(token)
            : Lock(token: token, function: core);
    }

    private Task<object> Read(bool locked, Func<string, CancellationToken, Task> onDeserializing, CancellationToken token) {
        async Task<object> core(CancellationToken token) {
            var json = await ReadString(token);
            var callback = onDeserializing?.Invoke(json, token);
            if (callback is not null) {
                await callback;
            }
            var obj = UnmakeString(json);
            return obj;
        }
        return locked == false
            ? core(token)
            : Lock(token: token, function: core);
    }

    private async Task<T> Read<T>(bool locked, Func<string, CancellationToken, Task> onDeserializing, CancellationToken token) {
        var obj = await Read(locked, onDeserializing, token);
        if (obj is null) {
            return default;
        }
        if (obj is T result) {
            return result;
        }
        throw new ObjectTypeUnexpectedException(
            expectedType: typeof(T),
            actualType: obj.GetType());
    }

    private string MakeString(object obj) {
        if (obj is null) {
            /*
             * Null instances are identified by empty strings.
             */
            return "";
        }
        var wrap = Wrap(obj);
        var json = Serialize(wrap);
        return json;
    }

    private object UnmakeString(string json) {
        if (string.IsNullOrWhiteSpace(json)) {
            /*
             * "Empty" strings identify null instances.
             */
            return null;
        }
        var wrap = Unwrap(json);
        return wrap.ObjectToken;
    }

    internal Task<string> Write(object obj,
                                Func<string, CancellationToken, Task> onSerialized = null,
                                CancellationToken token = default) {
        return Write(locked: true, obj, onSerialized, token);
    }

    internal Task<object> Read(Func<string, CancellationToken, Task> onDeserializing = null,
                               CancellationToken token = default) {
        return Read(locked: true, onDeserializing, token);
    }

    internal Task<T> Read<T>(Func<string, CancellationToken, Task> onDeserializing = null,
                             CancellationToken token = default) {
        return Read<T>(locked: true, onDeserializing, token);
    }

    internal Task<object> Call(object obj,
                               Func<string, CancellationToken, Task> onSerialized = null,
                               Func<string, CancellationToken, Task> onDeserializing = null,
                               CancellationToken token = default) {
        /*
         * This method performs a read/write operation inside a lock, guaranteeing a
         * sort-of atomicity of the operation.
         */
        return Lock(token: token, function: async token => {
            await Write(locked: false, obj, onSerialized, token);
            return await Read(locked: false, onDeserializing, token);
        });
    }

    internal static ObjectClient From(Stream stream, IObjectTypeResolver typeResolver) {
        /*
         * disposeStream is false because the caller owns the stream's
         * lifetime. For example, ObjectCallHost.Accepted owns the
         * TcpClient and disposes it (and its stream) via its own
         * using block.
         */
        return new(new ExistingStreamProvider(stream, disposeStream: false), typeResolver);
    }

    /// <summary>
    /// Creates an instance of <see cref="ObjectClient"/> from an IP end point.
    /// </summary>
    /// <param name="endPoint">The end point to be used by the client.</param>
    /// <param name="typeResolver">The instance used to resolve type information.</param>
    /// <returns>A new instance of <see cref="ObjectClient"/> for the specified IP end point.</returns>
    public static ObjectClient From(IPEndPoint endPoint, IObjectTypeResolver typeResolver) {
        return new(new TcpStreamProvider(endPoint), typeResolver);
    }

    /// <summary>
    /// Releases the resources used by the current instance of the class.
    /// </summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~ObjectClient() {
        Dispose(false);
    }

    /*
     * Two parallel wrapper hierarchies exist because serialization and
     * deserialization have different needs:
     *
     *   - The "Instance" wrappers (used during serialization) hold the
     *     object payload as 'object', letting Json.NET serialize it
     *     from the CLR instance directly.
     *
     *   - The "Json" wrappers (used during deserialization) hold the
     *     object payload as 'JToken', preserving raw JSON so the
     *     framework can resolve the CLR type from the envelope
     *     metadata before deserializing the payload.
     *
     * See the Unwrap and Serialize methods for how each is used.
     */

    private sealed class ObjectJsonWrapper {
        [JsonProperty(PropertyName = "t")]
        public string ObjectType { get; init; }

        [JsonProperty(PropertyName = "s")]
        public string ObjectSource { get; init; }

        [JsonProperty(PropertyName = "v")]
        public string ObjectVersion { get; init; }

        [JsonProperty(PropertyName = "o")]
        public JToken ObjectToken { get; init; }

        public ObjectTypeInfo ObjectTypeInfo() {
            return new(
                typeName: ObjectType,
                typeSource: ObjectSource,
                typeVersion: ObjectVersion);
        }
    }

    private sealed class ObjectInstanceWrapper {
        [JsonProperty(PropertyName = "t")]
        public string ObjectType { get; init; }

        [JsonProperty(PropertyName = "s")]
        public string ObjectSource { get; init; }

        [JsonProperty(PropertyName = "v")]
        public string ObjectVersion { get; init; }

        [JsonProperty(PropertyName = "o")]
        public object ObjectToken { get; init; }
    }

    private sealed class ApiJsonWrapper {
        [JsonProperty(PropertyName = "v", Order = 0)]
        public int ApiVersion { get; init; }

        [JsonProperty(PropertyName = "j", Order = 1)]
        public ObjectJsonWrapper Object { get; init; }
    }

    private sealed class ApiInstanceWrapper {
        [JsonProperty(PropertyName = "v", Order = 0)]
        public int ApiVersion { get; init; }

        [JsonProperty(PropertyName = "j", Order = 1)]
        public ObjectInstanceWrapper Object { get; init; }
    }
}
