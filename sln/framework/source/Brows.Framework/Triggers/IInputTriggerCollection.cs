using System.Collections.Generic;

namespace Brows.Triggers;

public interface IInputTriggerCollection : IReadOnlyCollection<IInputTrigger> {
    IInputTrigger Main { get; }
}
