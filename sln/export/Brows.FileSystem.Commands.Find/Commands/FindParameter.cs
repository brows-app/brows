using Domore.Conf;
using Domore.Conf.Cli;
using Domore.Conf.Converters;
using Domore.Text;
using System.Collections.Generic;

namespace Brows.Commands {
    internal sealed class FindParameter {
        [CliArgument]
        [CliRequired]
        public string Pattern { get; set; }
        public FindIn In { get; set; } = FindIn.FileName;
        public FindCaseSensitivity Case { get; set; }

        [ConfListItems(Separator = ";")]
        public List<string> Exclude {
            get => _Exclude ?? (_Exclude = new());
            set => _Exclude = value;
        }
        private List<string> _Exclude;

        [Conf("exclude-case")]
        public FindCaseSensitivity ExcludeCase { get; set; }

        [ConfListItems(Separator = ";")]
        public List<string> Include {
            get => _Include ?? (_Include = new());
            set => _Include = value;
        }
        private List<string> _Include;

        [Conf("include-case")]
        public FindCaseSensitivity IncludeCase { get; set; }

        [CliDisplay(false)]
        public DecodedTextOptions Decoder {
            get => _Decoder ?? (_Decoder = new());
            set => _Decoder = value;
        }
        private DecodedTextOptions _Decoder;

        public IMatcher PatternMatcher() {
            var match = MatchAlgorithm.Create(ignoreCase: Case == FindCaseSensitivity.None);
            var matcher = match.Matcher(Pattern);
            return matcher;
        }

        public FindFilter ExcludeFilter() {
            return new FindFilter { Case = ExcludeCase, List = Exclude };
        }

        public FindFilter IncludeFilter() {
            return new FindFilter { Case = IncludeCase, List = Include };
        }
    }
}
