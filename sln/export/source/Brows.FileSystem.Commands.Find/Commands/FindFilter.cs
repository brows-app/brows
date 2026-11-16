using System.Collections.Generic;
using System.Linq;

namespace Brows.Commands;

internal sealed record FindFilter {

    public FindCaseSensitivity Case { get; set; }

    public List<string> List {
        get => field ??= [];
        set;
    }

    public IEnumerable<IMatcher> Matchers() {
        var ignoreCase = Case == FindCaseSensitivity.None;
        var matchers = List.Select(s => new FindMatcher(s, ignoreCase));
        return matchers;
    }
}
