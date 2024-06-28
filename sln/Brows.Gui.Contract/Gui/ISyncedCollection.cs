using System;
using System.Collections;

namespace Brows.Gui {
    public interface ISyncedCollection : IEnumerable {
        void Sync(IEnumerable collection, object context, Action accessMethod, bool writeAccess);
    }
}
