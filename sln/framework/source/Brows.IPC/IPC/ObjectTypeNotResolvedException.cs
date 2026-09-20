namespace Brows.IPC;

internal sealed class ObjectTypeNotResolvedException : ObjectTypeException {
    public sealed override string Message =>
        $"The type from info '{Info}' could not be resolved.";

    public ObjectTypeInfo Info { get; }

    public ObjectTypeNotResolvedException(ObjectTypeInfo info) {
        Info = info;
    }
}
