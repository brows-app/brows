namespace Brows {
    public enum DeviceChangeKind {
        NodesChanged,
        ConfigChangeRequest,
        ConfigChanged,
        ConfigChangeCanceled,
        Arrival,
        RemovalRequest,
        RemovalCanceled,
        RemovalPending,
        RemovalComplete,
        TypeSpecific,
        Custom,
        UserDefined
    }
}
