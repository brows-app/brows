using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Triggers {
    internal sealed class GestureTriggerCollection : IGestureTriggerCollection {
        private readonly IReadOnlyDictionary<IGesture, IGestureTrigger> Set;

        private GestureTriggerCollection(IReadOnlyDictionary<IGesture, IGestureTrigger> set) {
            Set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public IGestureTrigger this[IGesture gesture] =>
            Set[gesture];

        public int Count =>
            Set.Count;

        public static GestureTriggerCollection From(IReadOnlyDictionary<IGesture, string> set) {
            if (null == set) throw new ArgumentNullException(nameof(set));
            return new GestureTriggerCollection(set.ToDictionary(
                g => g.Key,
                g => (IGestureTrigger)new GestureTrigger(g.Value, g.Key)));
        }

        public static GestureTriggerCollection Combine(params GestureTriggerCollection[] collections) {
            if (null == collections) throw new ArgumentNullException(nameof(collections));
            var set = new Dictionary<IGesture, IGestureTrigger>();
            foreach (var collection in collections) {
                foreach (var item in collection.Set) {
                    set[item.Key] = item.Value;
                }
            }
            return new GestureTriggerCollection(set);
        }

        public IEnumerator<IGestureTrigger> GetEnumerator() {
            return Set.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
