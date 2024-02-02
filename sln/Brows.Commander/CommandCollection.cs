using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class CommandCollection {
        private static readonly ILog Log = Logging.For(typeof(CommandCollection));

        private readonly List<ICommand> List;

        private Dictionary<IGesture, HashSet<ICommand>> GestureSet {
            get {
                if (_GestureSet == null) {
                    _GestureSet = new();
                    foreach (var item in List) {
                        var triggers = item.Trigger?.Gesture;
                        if (triggers != null) {
                            foreach (var trigger in triggers) {
                                var g = trigger.Gesture;
                                var list = _GestureSet.ContainsKey(g)
                                    ? (_GestureSet[g])
                                    : (_GestureSet[g] = new HashSet<ICommand>());
                                list.Add(item);
                            }
                        }
                    }
                }
                return _GestureSet;
            }
        }
        private Dictionary<IGesture, HashSet<ICommand>> _GestureSet;

        private Dictionary<string, HashSet<ICommand>> InputSet =>
            _InputSet ?? (
            _InputSet = new());
        private Dictionary<string, HashSet<ICommand>> _InputSet;

        public CommandCollection(IEnumerable<ICommand> items) {
            List = new List<ICommand>(items);
        }

        public bool Triggered(string input, out IReadOnlySet<ICommand> commands) {
            var s = input;
            if (InputSet.TryGetValue(s, out var list) == false) {
                InputSet[s] = list = List.Where(c => c.Trigger?.Input?.Triggered(s) ?? false).ToHashSet();
            }
            commands = list;
            return commands.Count > 0;
        }

        public bool Triggered(IGesture gesture, out IReadOnlySet<ICommand> commands) {
            if (GestureSet.TryGetValue(gesture, out var list)) {
                commands = list;
                return true;
            }
            commands = null;
            return false;
        }

        public async Task Init(CancellationToken token) {
            var tasks = new List<Task>(List.Select(async item => {
                try {
                    await item.Init(token);
                }
                catch (Exception ex) {
                    if (Log.Warn()) {
                        Log.Warn(ex);
                    }
                    List.Remove(item);
                }
            }));
            await Task.WhenAll(tasks);
        }

        public IEnumerable<ICommand> AsEnumerable() {
            return List.AsEnumerable();
        }

        public IEnumerator<ICommand> GetEnumerator() {
            return List.GetEnumerator();
        }
    }
}
