using Newtonsoft.Json.Serialization;

namespace Brows.Json;

public abstract class JsonShouldSerializeProvider {
    public abstract bool ShouldSerialize(JsonProperty property, object obj);
}
