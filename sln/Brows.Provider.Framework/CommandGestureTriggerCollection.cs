using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal sealed class CommandGestureTriggerCollection : IGestureTriggerCollection {
        private readonly IReadOnlyDictionary<IGesture, IGestureTrigger> Set;

        private CommandGestureTriggerCollection(IReadOnlyDictionary<IGesture, IGestureTrigger> set) {
            Set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public IGestureTrigger this[IGesture gesture] =>
            Set[gesture];

        public int Count =>
            Set.Count;

        public static CommandGestureTriggerCollection From(IReadOnlyDictionary<IGesture, string> set) {
            if (null == set) throw new ArgumentNullException(nameof(set));
            return new CommandGestureTriggerCollection(set.ToDictionary(
                g => g.Key,
                g => (IGestureTrigger)new CommandGestureTrigger(g.Key, g.Value)));
        }

        public static CommandGestureTriggerCollection Combine(params CommandGestureTriggerCollection[] collections) {
            if (null == collections) throw new ArgumentNullException(nameof(collections));
            var set = new Dictionary<IGesture, IGestureTrigger>();
            foreach (var collection in collections) {
                foreach (var item in collection.Set) {
                    set[item.Key] = item.Value;
                }
            }
            return new CommandGestureTriggerCollection(set);
        }

        IEnumerator<IGestureTrigger> IEnumerable<IGestureTrigger>.GetEnumerator() {
            return ((IEnumerable<IGestureTrigger>)Set.Values).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)Set.Values).GetEnumerator();
        }
    }
}
