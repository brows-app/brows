using System.Collections;
using System.Collections.Generic;

namespace Brows;

/*
 * The nesting names the archive entries that hold the archives that are
 * entered one after another. The archive that is stored in the archive of
 * the file system is browsed through the entries that a nesting names.
 */
internal sealed class SevenZipArchiveNest : IEnumerable<string> {
    private readonly List<string> List;

    public int Count =>
        List.Count;

    public SevenZipArchiveNest(IEnumerable<string> nesting) {
        List = new List<string>(nesting);
    }

    public IEnumerator<string> GetEnumerator() {
        return List.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
