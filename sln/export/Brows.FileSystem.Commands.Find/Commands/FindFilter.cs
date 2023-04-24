using System.Collections.Generic;
using System.Linq;

namespace Brows.Commands {
    internal sealed class FindFilter {
        public FindCaseSensitivity Case { get; set; }

        public List<string> List {
            get => _List ?? (_List = new());
            set => _List = value;
        }
        private List<string> _List;

        public IEnumerable<IMatcher> Matchers() {
            var match = MatchAlgorithm.Create(ignoreCase: Case == FindCaseSensitivity.None);
            var matchers = List.Select(s => match.Matcher(s));
            return matchers;
        }
    }
}
