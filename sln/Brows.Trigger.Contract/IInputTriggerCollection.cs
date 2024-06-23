using System.Collections.Generic;

namespace Brows {
    public interface IInputTriggerCollection : IReadOnlyCollection<IInputTrigger> {
        IInputTrigger Main { get; }
    }
}
