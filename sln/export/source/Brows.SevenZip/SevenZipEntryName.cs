using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows;

/// <summary>
/// Represents a name stored for an item inside a 7-Zip archive.
/// </summary>
public sealed class SevenZipEntryName {
    /*
     * 7-Zip stores the names of the items of an archive with forward slashes
     * as separators. The names that were made on Windows may also hold
     * backslashes, so both of them are treated as separators.
     */
    private static readonly char[] Separators = new[] { '/', '\\' };

    internal IReadOnlyList<string> Parts => field ??= Original.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

    internal string Normalized => field ??= string.Join(Path.DirectorySeparatorChar, Parts);

    internal string Top => field ??= (Parts.LastOrDefault() ?? "");

    internal string Parent => field ??= (
        Parts.Count == 0
            ? ""
            : string.Join(Path.DirectorySeparatorChar, Parts.Take(Parts.Count - 1)));

    internal IReadOnlyList<string> Paths {
        get {
            if (field is null) {
                var parts = Parts;
                var paths = new List<string>(capacity: parts.Count);
                for (var i = 0; i < parts.Count - 1; i++) {
                    paths.Add(string.Join(Path.DirectorySeparatorChar, parts.Take(i + 1)));
                }
                field = paths;
            }
            return field;
        }
    }

    internal SevenZipEntryName(string original) {
        Original = original ?? throw new ArgumentNullException(nameof(original));
    }

    /// <summary>
    /// Gets the name exactly as it is stored inside the archive.
    /// </summary>
    public string Original { get; }
}
