using System.Collections.Generic;

namespace Brows {
    public interface ITriggerPressCollection : IReadOnlyCollection<ITriggerPress> {
        ITriggerPress this[PressGesture gesture] { get; }
    }
}
