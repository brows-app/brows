using System.Collections.Generic;

namespace Brows {
    public interface IGestureTriggerCollection : IReadOnlyCollection<IGestureTrigger> {
        IGestureTrigger this[IGesture gesture] { get; }
    }
}
