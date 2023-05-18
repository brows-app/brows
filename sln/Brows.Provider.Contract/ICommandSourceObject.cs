using System.Collections;

namespace Brows {
    public interface ICommandSourceObject {
        public object Instance { get; }
        public IEnumerable Collection { get; }
    }
}
