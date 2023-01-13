using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public class CommandCollection : ICommandCollection {
        private readonly List<ICommand> List;

        private Dictionary<PressGesture, HashSet<ICommand>> PressSet {
            get {
                if (_PressSet == null) {
                    _PressSet = new Dictionary<PressGesture, HashSet<ICommand>>();
                    foreach (var item in List) {
                        var press = item.Trigger?.Press;
                        if (press != null) {
                            foreach (var p in press) {
                                var gesture = p.Gesture;
                                var list = _PressSet.ContainsKey(gesture) ?
                                    _PressSet[gesture] :
                                    _PressSet[gesture] = new HashSet<ICommand>();
                                list.Add(item);
                            }
                        }
                    }
                }
                return _PressSet;
            }
        }
        private Dictionary<PressGesture, HashSet<ICommand>> _PressSet;

        private Dictionary<string, HashSet<ICommand>> InputSet =>
            _InputSet ?? (
            _InputSet = new());
        private Dictionary<string, HashSet<ICommand>> _InputSet;

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

        public bool Triggered(string input, out IReadOnlySet<ICommand> commands) {
            var s = input;
            if (InputSet.TryGetValue(s, out var list) == false) {
                InputSet[s] = list = List.Where(c => c.Trigger?.Input?.Triggered(s) ?? false).ToHashSet();
            }
            commands = list;
            return commands.Count > 0;
        }

        public bool Triggered(PressGesture press, out IReadOnlySet<ICommand> commands) {
            if (PressSet.TryGetValue(press, out var list)) {
                commands = list;
                return true;
            }
            commands = null;
            return false;
        }

        public async Task Init(CancellationToken cancellationToken) {
            await Task.WhenAll(List.Select(item => item.Init(cancellationToken)));
        }

        IEnumerator<ICommand> IEnumerable<ICommand>.GetEnumerator() {
            return ((IEnumerable<ICommand>)List).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)List).GetEnumerator();
        }
    }
}
