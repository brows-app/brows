using System.Collections;
using System.Collections.Generic;

namespace Brows {
    public sealed class ZipArchiveNest : IEnumerable<string> {
        private readonly List<string> List;

        public int Count =>
            List.Count;

        public ZipArchiveNest(IEnumerable<string> nesting) {
            List = new List<string>(nesting);
        }

        public IEnumerator<string> GetEnumerator() {
            return ((IEnumerable<string>)List).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)List).GetEnumerator();
        }
    }
}
