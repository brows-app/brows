using Brows.Config;
using Domore.Conf;
using Domore.IO;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
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
            set {
                _GestureSet = null;
            }
        }
        private Dictionary<IGesture, HashSet<ICommand>> _GestureSet;

        private Dictionary<string, HashSet<ICommand>> InputSet {
            get => _InputSet ??= [];
            set => _InputSet = value;
        }
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
            if (Log.Info()) {
                Log.Info(nameof(Init));
            }
            var configPath = Path.Join(ConfigPath.FileRoot, "commands.conf");
            var configFile = await FileSystemTask.ExistingFile(configPath, token);
            var config = configFile == null ? null : Conf.Contain(configFile.FullName);
            var items = new List<ICommand>(List);
            await Task.WhenAll(items.Select(async command => {
                if (config != null) {
                    if (Log.Info()) {
                        Log.Info(Log.Join(nameof(config.Configure), command));
                    }
                    var type = command.GetType();
                    var name = type.Name;
                    var assembly = type.Assembly?.GetName()?.Name;
                    var namespac = type.Namespace;
                    config.Configure(command.Config, key: name);
                    config.Configure(command.Config, key: name + $"[{assembly}]");
                    config.Configure(command.Config, key: name + $"[{assembly}][{namespac}]");
                }
                try {
                    await command.Init(token);
                }
                catch (Exception ex) {
                    if (Log.Warn()) {
                        Log.Warn(ex);
                    }
                    List.Remove(command);
                    return;
                }
                if (command.Enabled == false) {
                    if (Log.Info()) {
                        Log.Info(Log.Join(command, nameof(command.Enabled), command.Enabled));
                    }
                    List.Remove(command);
                    return;
                }
                command.TriggerChanged += (s, e) => {
                    InputSet = null;
                    GestureSet = null;
                };
            }));
        }

        public IEnumerable<ICommand> AsEnumerable() {
            return List.AsEnumerable();
        }

        public IEnumerator<ICommand> GetEnumerator() {
            return List.GetEnumerator();
        }
    }
}
