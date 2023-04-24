using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Config;

    internal sealed class CommandTrigger : ICommandTrigger {
        private CommandTrigger(IInputTrigger input, IGestureTriggerCollection gesture) {
            Input = input;
            Gesture = gesture;
        }

        public static async Task<CommandTrigger> For(Command command, CancellationToken cancellationToken) {
            if (null == command) throw new ArgumentNullException(nameof(command));
            var name = command.Name;
            var conf = await Configure.File<Trigger>().Load(cancellationToken);
            if (conf.Cmd.TryGetValue(name, out var cmd) == false) {
                var type = command.GetType();
                if (conf.Cmd.TryGetValue(type.Name, out cmd) == false) {
                    return null;
                }
            }
            return new CommandTrigger(cmd.TriggerInput(), cmd.TriggerGesture());
        }

        public IInputTrigger Input { get; }
        public IGestureTriggerCollection Gesture { get; }

        private class Trigger {
            public Dictionary<string, Command> Cmd {
                get => _Cmd ?? (_Cmd = new(StringComparer.OrdinalIgnoreCase));
                set => _Cmd = value;
            }
            private Dictionary<string, Command> _Cmd;

            public class Command {
                private static CommandGestureTriggerCollection Triggers<TGesture>(string input, Dictionary<string, string> set) where TGesture : IGesture, new() {
                    if (null == set) throw new ArgumentNullException(nameof(set));
                    return CommandGestureTriggerCollection.From(set
                        .SelectMany(pair => pair.Key
                            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(key => (key: key, shortcut: pair.Value)))
                        .ToDictionary(
                            pair => new TGesture().Parse(pair.key),
                            pair => pair.shortcut.Replace(
                                "*",
                                input?.Split('|')?.FirstOrDefault()?.Trim() ?? "*")));
                }

                public string Input { get; set; }

                public Dictionary<string, string> Press {
                    get => _Press ?? (_Press = new());
                    set => _Press = value;
                }
                private Dictionary<string, string> _Press;

                public Dictionary<string, string> Click {
                    get => _Click ?? (_Click = new());
                    set => _Click = value;
                }
                private Dictionary<string, string> _Click;

                public IInputTrigger TriggerInput() {
                    var input = Input?.Trim() ?? "";
                    if (input == "") {
                        return null;
                    }
                    var parts = input.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    return new CommandTriggerInput(parts[0], parts.Skip(1).ToArray());
                }

                public CommandGestureTriggerCollection TriggerGesture() {
                    var press = Triggers<PressGesture>(Input, Press);
                    var click = Triggers<ClickGesture>(Input, Click);
                    return CommandGestureTriggerCollection.Combine(press, click);
                }
            }
        }
    }
}
