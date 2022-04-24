using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    using Triggers;

    public class CommandCollection : ICommandCollection {
        private readonly List<ICommand> List;

        private Dictionary<KeyboardGesture, HashSet<ICommand>> KeyboardDictionary {
            get {
                if (_KeyboardDictionary == null) {
                    _KeyboardDictionary = new Dictionary<KeyboardGesture, HashSet<ICommand>>();
                    foreach (var item in List) {
                        foreach (var keyboardTrigger in item.KeyboardTriggers) {
                            var gesture = keyboardTrigger.Gesture;
                            var list = _KeyboardDictionary.ContainsKey(gesture) ?
                                _KeyboardDictionary[gesture] :
                                _KeyboardDictionary[gesture] = new HashSet<ICommand>();
                            list.Add(item);
                        }
                    }
                }
                return _KeyboardDictionary;
            }
        }
        private Dictionary<KeyboardGesture, HashSet<ICommand>> _KeyboardDictionary;

        private Dictionary<string, HashSet<ICommand>> InputDictionary {
            get {
                if (_InputDictionary == null) {
                    _InputDictionary = new Dictionary<string, HashSet<ICommand>>();
                }
                return _InputDictionary;
            }
        }
        private Dictionary<string, HashSet<ICommand>> _InputDictionary;

        public CommandCollection(IEnumerable<ICommand> items) {
            List = new List<ICommand>(items);
        }

        public bool Arbitrary(out IReadOnlySet<ICommand> commands) {
            var arbitrary = List.Where(item => item.Arbitrary).ToHashSet();
            if (arbitrary.Count > 0) {
                commands = arbitrary;
                return true;
            }
            commands = null;
            return false;
        }

        public bool Triggered(InputTrigger trigger, out IReadOnlySet<ICommand> commands) {
            if (null == trigger) throw new ArgumentNullException(nameof(trigger));
            var s = trigger.Value;
            if (InputDictionary.TryGetValue(s, out var list) == false) {
                InputDictionary[s] = list = List.Where(c => c.InputTriggers.Any(t => t.Triggered(s))).ToHashSet();
            }
            if (list.Count > 0) {
                commands = list;
                return true;
            }
            commands = null;
            return false;
        }

        public bool Triggered(KeyboardTrigger trigger, out IReadOnlySet<ICommand> commands) {
            if (null == trigger) throw new ArgumentNullException(nameof(trigger));
            if (KeyboardDictionary.TryGetValue(trigger.Gesture, out var list)) {
                commands = list;
                return true;
            }
            commands = null;
            return false;
        }

        public bool Triggered(ITrigger trigger, out IReadOnlySet<ICommand> commands) {
            if (trigger is InputTrigger i) {
                return Triggered(i, out commands);
            }
            if (trigger is KeyboardTrigger k) {
                return Triggered(k, out commands);
            }
            throw new ArgumentException(paramName: nameof(trigger), message: $"{nameof(trigger)} [{trigger}]");
        }

        IEnumerator<ICommand> IEnumerable<ICommand>.GetEnumerator() {
            return ((IEnumerable<ICommand>)List).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)List).GetEnumerator();
        }
    }
}
