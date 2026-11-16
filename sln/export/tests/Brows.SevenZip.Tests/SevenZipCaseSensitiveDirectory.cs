using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Brows;

internal static class SevenZipCaseSensitiveDirectory {
    private const int FileCaseSensitiveInfoClass = 23;
    private const uint FileCaseSensitiveDirectory = 0x00000001;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileWriteAttributes = 0x00000100;
    private const uint OpenExisting = 3;
    private const uint ShareDelete = 0x00000004;
    private const uint ShareRead = 0x00000001;
    private const uint ShareWrite = 0x00000002;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle file,
        int fileInformationClass,
        ref FileCaseSensitiveInfo fileInformation,
        uint bufferSize);

    public static bool TryEnable(string directory) {
        using var file = CreateFile(directory,
            FileWriteAttributes,
            ShareRead | ShareWrite | ShareDelete,
            0,
            OpenExisting,
            FileFlagBackupSemantics,
            0);
        if (file.IsInvalid) {
            return false;
        }
        var info = new FileCaseSensitiveInfo {
            Flags = FileCaseSensitiveDirectory
        };
        return SetFileInformationByHandle(
            file,
            FileCaseSensitiveInfoClass,
            ref info,
            (uint)Marshal.SizeOf<FileCaseSensitiveInfo>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileCaseSensitiveInfo {
        public uint Flags;
    }
}
