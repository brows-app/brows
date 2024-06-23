using System.Collections.Generic;

namespace Brows {
    public interface IInputTrigger : ITrigger {
        string Input { get; }
        IReadOnlySet<string> Aliases { get; }
        IReadOnlySet<string> Options { get; }
        bool Triggered(string s);
    }
}
