using System.Collections.Generic;

namespace Brows.Matching;

public interface IMatched {
    IEnumerable<IMatch> Matches { get; }
}
