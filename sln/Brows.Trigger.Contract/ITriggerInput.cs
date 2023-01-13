using System.Collections.Generic;

namespace Brows {
    public interface ITriggerInput : ITrigger {
        string String { get; }
        IReadOnlySet<string> Aliases { get; }
        IReadOnlySet<string> Options { get; }
        bool Triggered(string s);
    }
}
