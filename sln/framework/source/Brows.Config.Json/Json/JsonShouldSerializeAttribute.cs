using Domore.Logs;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Concurrent;

namespace Brows.Json;

[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonShouldSerializeAttribute : Attribute {
    private static readonly ILog Log = Logging.For(typeof(JsonShouldSerializeAttribute));

    private static readonly ConcurrentDictionary<Type, JsonShouldSerializeProvider> Providers = [];

    private bool ShouldSerialize(JsonProperty property, object obj) {
        var provider = Providers.GetOrAdd(ProviderType, type => {
            try {
                return Activator.CreateInstance(type) as JsonShouldSerializeProvider;
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(ex);
                }
                return null;
            }
        });
        if (provider is not null) {
            return provider.ShouldSerialize(property, obj);
        }
        return true;
    }

    public Type ProviderType { get; }

    public JsonShouldSerializeAttribute(Type providerType) {
        ProviderType = providerType;
    }

    public static void Register(JsonProperty property) {
        if (property is null) {
            throw new ArgumentNullException(nameof(property));
        }
        var attributeProvider = property.AttributeProvider;
        if (attributeProvider is null) {
            return;
        }
        var attributes = attributeProvider.GetAttributes(typeof(JsonShouldSerializeAttribute), inherit: true);
        if (attributes is null || attributes.Count == 0) {
            return;
        }
        property.ShouldSerialize = obj => {
            foreach (var attribute in attributes) {
                if (attribute is JsonShouldSerializeAttribute self) {
                    var shouldSerialize = self.ShouldSerialize(property, obj);
                    if (shouldSerialize == false) {
                        return false;
                    }
                }
            }
            return true;
        };
    }
}
