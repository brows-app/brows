using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Brows.Composition {
    internal class ProgramPart {
        [ImportMany]
        public IEnumerable<IProgram> Collection {
            get => _Collection ?? (_Collection = Array.Empty<IProgram>());
            set => _Collection = value;
        }
        private IEnumerable<IProgram> _Collection;
    }
}
