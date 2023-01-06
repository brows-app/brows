using System.Collections.Generic;

namespace Brows {
    public interface IEntryView {
        IReadOnlyList<string> Columns { get; }
    }
}
