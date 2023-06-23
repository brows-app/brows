using Domore.Conf.Cli;
using System.Collections.Generic;

namespace Brows.Commands {
    internal sealed class MetadataParameter {
        [CliParameters]
        public Dictionary<string, string> Set {
            get => _Set ?? (_Set = new());
            set => _Set = value;
        }
        private Dictionary<string, string> _Set;
    }
}
