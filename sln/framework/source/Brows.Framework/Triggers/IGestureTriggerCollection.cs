using System.Collections.Generic;

namespace Brows.Triggers;

public interface IGestureTriggerCollection : IReadOnlyCollection<IGestureTrigger> {
    IGestureTrigger this[IGesture gesture] { get; }
}
