using Brows.Config;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class CommandTrigger : ICommandTrigger {
        private static readonly ILog Log = Logging.For(typeof(CommandTrigger));
        private static readonly SemaphoreSlim Locker = new(1, 1);

        private static IConfig<Trigger> Config {
            get {
                if (_Config == null) {
                    _Config = Configure.File<Trigger>();
                    _Config.Changed += Config_Changed;
                }
                return _Config;
            }
        }
        private static IConfig<Trigger> _Config;

        private CommandTrigger(IInputTrigger input, IGestureTriggerCollection gesture) {
            Input = input;
            Gesture = gesture;
        }

        private static async void Config_Changed(object sender, EventArgs e) {
            if (Log.Info()) {
                Log.Info(nameof(Config_Changed));
            }
            await Locker.WaitAsync();
            try {
                await Config.Load(default);
                Changed?.Invoke(null, e);
            }
            finally {
                Locker.Release();
            }
        }

        public static event EventHandler Changed;

        public IInputTrigger Input { get; }
        public IGestureTriggerCollection Gesture { get; }

        public static CommandTrigger For(Command command) {
            if (null == command) throw new ArgumentNullException(nameof(command));
            var name = command.Name;
            var conf = Config.Loaded;
            var confCmd = conf?.Cmd;
            if (confCmd == null) {
                return null;
            }
            if (confCmd.TryGetValue(name, out var cmd) == false) {
                var type = command.GetType();
                if (confCmd.TryGetValue(type.Name, out cmd) == false) {
                    return null;
                }
            }
            return new CommandTrigger(cmd.TriggerInput(), cmd.TriggerGesture());
        }

        public static async Task<CommandTrigger> For(Command command, CancellationToken token) {
            await Config.Load(token);
            return For(command);
        }

        private sealed class Trigger {
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
