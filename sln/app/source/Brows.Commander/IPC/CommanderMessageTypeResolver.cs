using Brows.IPC.Messages;

namespace Brows.IPC;

internal sealed class CommanderMessageTypeResolver : IObjectTypeResolver {
    private readonly ObjectTypeInfo Browse = new("b");

    ObjectTypeInfo IObjectTypeResolver.Reverse(Type type) {
        if (type == typeof(Browse)) {
            return Browse;
        }
        return null;
    }

    Type IObjectTypeResolver.Resolve(ObjectTypeInfo info) {
        var typeName = info?.TypeName?.Trim() ?? "";
        if (typeName == "") {
            return null;
        }
        if (typeName.Equals(Browse.TypeName, StringComparison.OrdinalIgnoreCase)) {
            return typeof(Browse);
        }
        return null;
    }
}
