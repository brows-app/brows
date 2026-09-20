using System.Collections;

namespace Brows.Commands;

public interface ICommandSource {
    public object Item { get; }
    public IEnumerable Items { get; }
}
