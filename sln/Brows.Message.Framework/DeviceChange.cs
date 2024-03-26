namespace Brows {
    public sealed class DeviceChange : Message {
        public sealed override MessageType Type =>
            MessageType.DeviceChange;

        public DeviceChangeKind Kind { get; init; }
        public DeviceChangeInfo Info { get; init; }

        public sealed override string ToString() {
            return $"[{Kind}] {Info}";
        }
    }
}
