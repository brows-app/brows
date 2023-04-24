using Domore.Conf.Cli;
using System.Collections.Generic;

namespace Brows.Commands.DataKey {
    internal abstract class DataKeyCommandParameter {
        [CliRequired]
        [CliArguments]
        public IReadOnlyList<string> Args {
            get => _Args ?? (_Args = new List<string>());
            set => _Args = value;
        }
        private IReadOnlyList<string> _Args;
    }
}
