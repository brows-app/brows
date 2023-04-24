using System.Collections.Generic;

namespace Brows {
    internal sealed class CommanderConfig {
        public List<string> LoadFirst {
            get => _LoadFirst ?? (_LoadFirst = new());
            set => _LoadFirst = value;
        }
        private List<string> _LoadFirst;
    }
}
