using System.Collections;

namespace Brows {
    public interface ICommandSource {
        public object Item { get; }
        public IEnumerable Items { get; }
    }
}
