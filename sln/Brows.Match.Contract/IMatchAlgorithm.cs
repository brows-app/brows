using System;
using System.Collections.Generic;
using System.Threading;

namespace Brows {
    public interface IMatchAlgorithm {
        IMatcher Matcher(string pattern);
        IAsyncEnumerable<T> Match<T>(string pattern, IEnumerable<T> source, Func<T, string> selector, CancellationToken cancellationToken);
    }
}
