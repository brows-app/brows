using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Brows.IO.Compression {
    internal static class ZipArchiveEntryExtension {
        private static readonly IReadOnlyList<char> DirectorySeparatorChars = new[] {
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar };

        public static bool IsDirectory(this ZipArchiveEntry zipArchiveEntry) {
            ArgumentNullException.ThrowIfNull(zipArchiveEntry);
            var fullName = zipArchiveEntry.FullName;
            if (fullName != null) {
                var sepChars = DirectorySeparatorChars;
                if (sepChars.Any(c => fullName.EndsWith(c))) {
                    if (zipArchiveEntry.Length == 0) {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
