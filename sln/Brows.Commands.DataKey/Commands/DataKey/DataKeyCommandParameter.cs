using System;
using System.Collections.Generic;

namespace Brows.Commands.DataKey {
    using Cli;

    internal class DataKeyCommandParameter {
        [CommandArgument(Name = "keys", Aggregate = true)]
        public string Keys { get; set; }

        public IReadOnlyList<string> List =>
            _List ?? (
            _List = Keys?.Split() ?? Array.Empty<string>());
        private IReadOnlyList<string> _List;
    }
}
