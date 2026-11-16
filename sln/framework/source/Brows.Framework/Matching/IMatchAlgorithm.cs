using System;
using System.Collections.Generic;

namespace Brows.Matching;

public interface IMatchAlgorithm {
    IMatcher Matcher(string pattern);
    IAsyncEnumerable<T> Match<T>(string pattern, IEnumerable<T> source, Func<T, string> selector, CancellationToken cancellationToken);
}
