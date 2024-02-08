using System.IO;

namespace Brows.Commands {
    public static class DirectoryX {
        private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();
        private static readonly char DirectorySeparatorChar = Path.DirectorySeparatorChar;
        private static readonly char AltDirectorySeparatorChar = Path.AltDirectorySeparatorChar;

        public static bool Invalid(string path) {
            if (path is null) return true;
            if (path == string.Empty) return true;
            var pLen = path.Length;
            var invalid = InvalidPathChars;
            var invalidLen = invalid.Length;
            for (var p = 0; p < pLen; p++) {
                for (var i = 0; i < invalidLen; i++) {
                    if (invalid[i] == path[p]) {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool Separated(string path) {
            if (path is null) return false;
            if (path == string.Empty) return false;
            var pLen = path.Length;
            for (var i = 0; i < pLen; i++) {
                var c = path[i];
                if (c == DirectorySeparatorChar) {
                    return true;
                }
                if (c == AltDirectorySeparatorChar) {
                    return true;
                }
            }
            return false;
        }
    }
}
