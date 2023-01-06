using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Brows.Composition {
    internal class ComponentResourcePart {
        [ImportMany]
        public IEnumerable<IComponentResource> Collection {
            get => _Collection ?? (_Collection = Array.Empty<IComponentResource>());
            set => _Collection = value;
        }
        private IEnumerable<IComponentResource> _Collection;
    }
}
