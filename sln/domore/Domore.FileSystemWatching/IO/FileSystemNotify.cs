using System.IO;

namespace Domore.IO {
    internal static class FileSystemNotify {
        public const NotifyFilters All =
            NotifyFilters.Attributes |
            NotifyFilters.CreationTime |
            NotifyFilters.DirectoryName |
            NotifyFilters.FileName |
            NotifyFilters.LastAccess |
            NotifyFilters.LastWrite |
            NotifyFilters.Security |
            NotifyFilters.Size;
    }
}
