using System.Collections.Generic;

namespace Brows {
    internal class EntryView : IEntryView {
        public IReadOnlyList<string> Columns { get; }

        public EntryView(IReadOnlyList<string> columns) {
            Columns = columns;
        }
    }
}
