using System.Collections.Generic;

namespace Brows {
    public interface IProgramCommand {
        string Line { get; }
        IReadOnlyList<string> Args { get; }
    }
}
