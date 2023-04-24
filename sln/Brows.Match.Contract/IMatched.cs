using System.Collections.Generic;

namespace Brows {
    public interface IMatched {
        IEnumerable<IMatch> Matches { get; }
    }
}
