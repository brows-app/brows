using System.Collections;

namespace Brows.Commands;

public interface ICommandSourceObject {
    public object Instance { get; }
    public IEnumerable Collection { get; }
}
