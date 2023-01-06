using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Brows.Composition {
    internal class CommandPart {
        [ImportMany]
        public IEnumerable<ICommand> Collection {
            get => _Collection ?? (_Collection = Array.Empty<ICommand>());
            set => _Collection = value;
        }
        private IEnumerable<ICommand> _Collection;
    }
}
