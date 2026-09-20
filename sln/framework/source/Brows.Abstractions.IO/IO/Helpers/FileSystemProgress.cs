using System.IO;

namespace Brows.IO.Helpers;

public abstract class FileSystemProgress {
    public abstract void AddToTarget(long value);
    public abstract void AddToProgress(long value);
    public abstract void SetCurrentInfo(FileSystemInfo value);
}
