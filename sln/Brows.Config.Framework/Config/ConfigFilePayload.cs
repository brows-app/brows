namespace Brows.Config {
    internal sealed class ConfigFilePayload {
        public string ID { get; init; }
        public string Type { get; init; }
        public object Data { get; init; }
    }
}
