using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows {
    public sealed class ZipEntryName {
        private static readonly char[] DirectorySeparatorChars = new[] {
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        };

        public string Original { get; }

        public ZipEntryName(string original) {
            Original = original ?? throw new ArgumentNullException(nameof(original));
        }

        public IReadOnlyList<string> Parts =>
            _Parts ?? (
            _Parts = Original.Split(DirectorySeparatorChars, StringSplitOptions.RemoveEmptyEntries));
        private IReadOnlyList<string> _Parts;

        public string Normalized =>
            _Normalized ?? (
            _Normalized = string.Join(Path.DirectorySeparatorChar, Parts));
        private string _Normalized;

        public string Top =>
            _Top ?? (
            _Top = Parts.LastOrDefault() ?? "");
        private string _Top;

        public string Parent =>
            _Parent ?? (
            _Parent = Parts.Count == 0
                ? ""
                : string.Join(Path.DirectorySeparatorChar, Parts.Take(Parts.Count - 1)));
        private string _Parent;

        public IReadOnlyList<string> Paths {
            get {
                if (_Paths == null) {
                    var parts = Parts;
                    var paths = new List<string>(capacity: parts.Count);
                    for (var i = 0; i < parts.Count - 1; i++) {
                        paths.Add(string.Join(Path.DirectorySeparatorChar, parts.Take(i + 1)));
                    }
                    _Paths = paths;
                }
                return _Paths;
            }
        }
        private IReadOnlyList<string> _Paths;
    }
}
