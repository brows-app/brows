using System.Collections.Generic;

namespace Brows {
    public interface IProgramCommand {
        string CommandLine { get; }
        IReadOnlyList<string> Args { get; }
        T Configure<T>(T target);
    }
}
