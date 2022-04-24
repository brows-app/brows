using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Brows.ComponentModel.Composition {
    public class EntryProviderFactoryPart {
        [ImportMany]
        public IEnumerable<IEntryProviderFactory> Collection {
            get => _Collection ?? (_Collection = Array.Empty<IEntryProviderFactory>());
            set => _Collection = value;
        }
        private IEnumerable<IEntryProviderFactory> _Collection;
    }
}
