using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal class CommandTriggerPressCollection : ITriggerPressCollection {
        private readonly IReadOnlyDictionary<PressGesture, ITriggerPress> Set;

        private CommandTriggerPressCollection(IReadOnlyDictionary<PressGesture, ITriggerPress> set) {
            Set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public ITriggerPress this[PressGesture gesture] =>
            Set[gesture];

        public int Count =>
            Set.Count;

        public static CommandTriggerPressCollection From(IReadOnlyDictionary<PressGesture, string> set) {
            if (null == set) throw new ArgumentNullException(nameof(set));
            return new CommandTriggerPressCollection(set.ToDictionary(
                g => g.Key,
                g => (ITriggerPress)new CommandTriggerPress(g.Key, g.Value)));
        }

        IEnumerator<ITriggerPress> IEnumerable<ITriggerPress>.GetEnumerator() {
            return ((IEnumerable<ITriggerPress>)Set.Values).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)Set.Values).GetEnumerator();
        }
    }
}
