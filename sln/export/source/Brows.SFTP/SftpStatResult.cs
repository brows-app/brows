using Brows.Native;
using Brows.SFTP.Native;

namespace Brows;

internal readonly record struct SftpStatResult(
    SftpMetadata Metadata,
    uint? ErrorStatus,
    NativeErrorException Error);
