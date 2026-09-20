using Newtonsoft.Json.Serialization;
using System;
using System.Collections;

namespace Brows.Json.ShouldSerializeProviders;

public sealed class OnlyIfNotEmpty : JsonShouldSerializeProvider {
    public override bool ShouldSerialize(JsonProperty property, object obj) {
        if (property is null) {
            throw new ArgumentNullException(nameof(property));
        }
        var value = property.ValueProvider?.GetValue(obj);
        if (value is null) {
            return false;
        }
        if (value is IEnumerable collection) {
            foreach (var _ in collection) {
                return true;
            }
            return false;
        }
        return true;
    }
}
