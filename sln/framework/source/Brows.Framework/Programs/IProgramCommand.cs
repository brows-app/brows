using System.Collections.Generic;

namespace Brows.Programs;

public interface IProgramCommand {
    string CommandLine { get; }
    IReadOnlyList<string> Args { get; }
    T Configure<T>(T target);
}
