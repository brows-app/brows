using System.Collections.Generic;

namespace Brows;

internal sealed class CommanderConfig {
    public List<string> LoadFirst {
        get => field ??= [];
        set;
    }
}
