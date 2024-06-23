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

        private Dictionary<IGesture, IReadOnlyDictionary<IGestureTrigger, ICommand>> GestureSet {
            get => _GestureSet ??= [];
            set => _GestureSet = value;
        }
        private Dictionary<IGesture, IReadOnlyDictionary<IGestureTrigger, ICommand>> _GestureSet;

        private Dictionary<string, IReadOnlyDictionary<IInputTrigger, ICommand>> InputSet {
            get => _InputSet ??= [];
            set => _InputSet = value;
        }
        private Dictionary<string, IReadOnlyDictionary<IInputTrigger, ICommand>> _InputSet;

        public CommandCollection(IEnumerable<ICommand> items) {
            List = new List<ICommand>(items);
        }

        public bool Triggered(string input, out IReadOnlyDictionary<IInputTrigger, ICommand> commands) {
            if (input == null) {
                commands = null;
                return false;
            }
            var inputSet = InputSet;
            if (inputSet.TryGetValue(input, out var set) == false) {
                inputSet[input] = set = List
                    .Select(command =>
                        command.Trigger?.Triggered(input, out var trigger) == true
                            ? KeyValuePair.Create(trigger, command)
                            : default(KeyValuePair<IInputTrigger, ICommand>?))
                    .Where(item => item.HasValue)
                    .ToDictionary(item => item.Value.Key, item => item.Value.Value);
            }
            commands = set;
            return commands.Count > 0;
        }

        public bool Triggered(IGesture gesture, out IReadOnlyDictionary<IGestureTrigger, ICommand> commands) {
            if (gesture == null) {
                commands = null;
                return false;
            }
            var gestureSet = GestureSet;
            if (gestureSet.TryGetValue(gesture, out var set) == false) {
                gestureSet[gesture] = set = List
                    .Select(command =>
                        command.Trigger?.Triggered(gesture, out var trigger) == true
                            ? KeyValuePair.Create(trigger, command)
                            : default(KeyValuePair<IGestureTrigger, ICommand>?))
                    .Where(item => item.HasValue)
                    .ToDictionary(item => item.Value.Key, item => item.Value.Value);
            }
            commands = set;
            return commands.Count > 0;
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
