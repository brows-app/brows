using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Brows.MatchAlgorithms {
    internal sealed class RegularExpressionMatched : IMatched {
        public IEnumerable<IMatch> Matches =>
            Collection
                .Select(match => new RegularExpressionMatch(match))
                .Cast<IMatch>();

        public MatchCollection Collection { get; }

        public RegularExpressionMatched(MatchCollection collection) {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }
    }
}
