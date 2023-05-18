using Brows.Exports;

namespace Brows.FileSystem {
    public interface IFileSystemNavigationService {
        public IFileSystemIcon FileSystemIcon { get; }
        public IDriveIcon DriveIcon { get; }
    }
}
