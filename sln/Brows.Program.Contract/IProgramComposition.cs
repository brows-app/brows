using System.Collections.Generic;

namespace Brows {
    public interface IProgramComposition {
        IEnumerable<T> OfType<T>();
    }
}
