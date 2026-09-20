using Newtonsoft.Json;
using Brows.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config.FileSerializers;

/// <summary>
/// This implementation of <see cref="ConfigFileSerializer"/> serializes to and from
/// .json files, which is a transfer format for data objects.
/// </summary>
internal sealed class DataSerializer : ConfigFileSerializer {
    private static JsonSerializerSettings Settings() {
        var factory = Imports.Current.Find<IJsonSerializerSettingsFactory>(throwIfNotFound: false, throwIfNotReady: false);
        if (factory is null) {
            factory = JsonSerializerSettingsFactory.Default;
        }
        return factory.Create(settings => {
            settings.Formatting = Formatting.Indented;
        });
    }

    public sealed override string Extension => ".json";

    public sealed override string Serialize(object value) {
        return JsonConvert.SerializeObject(value, Formatting.Indented, Settings());
    }

    public sealed override T Deserialize<T>(string value) {
        return JsonConvert.DeserializeObject<T>(value, Settings());
    }

    public sealed override void Populate(object target, string text) {
        JsonConvert.PopulateObject(text, target, Settings());
    }

    public sealed override Task<IDisposable> Watch(string path,
                                                   object target,
                                                   Action configured,
                                                   Action<Exception> errored,
                                                   CancellationToken token) {
        throw new NotSupportedException();
    }
}
