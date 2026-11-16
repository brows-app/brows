namespace Brows;

internal static class SftpPosixMode {
    private const uint TypeMask = 0xF000;
    private const uint DirectoryType = 0x4000;
    private const uint SymbolicLinkType = 0xA000;

    internal static bool IsDirectory(uint permissions) {
        return (permissions & TypeMask) == DirectoryType;
    }

    internal static bool IsSymbolicLink(uint permissions) {
        return (permissions & TypeMask) == SymbolicLinkType;
    }
}
