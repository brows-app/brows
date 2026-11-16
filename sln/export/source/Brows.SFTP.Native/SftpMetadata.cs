namespace Brows.SFTP.Native;

internal sealed record SftpMetadata(string Name,
                                     uint Flags,
                                     ulong Size,
                                     uint Uid,
                                     uint Gid,
                                     uint Permissions,
                                     uint Atime,
                                     uint Mtime);
