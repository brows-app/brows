using Brows.Config;
using Brows.Triggers;
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

        private CommandTrigger(InputTriggerCollection inputs, GestureTriggerCollection gestures) {
            Inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
            Gestures = gestures ?? throw new ArgumentNullException(nameof(gestures));
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

        public InputTriggerCollection Inputs { get; }
        public GestureTriggerCollection Gestures { get; }

        IInputTriggerCollection ICommandTrigger.Inputs => Inputs;
        IGestureTriggerCollection ICommandTrigger.Gestures => Gestures;

        bool ICommandTrigger.Triggered(string s, out IInputTrigger trigger) {
            foreach (var item in Inputs) {
                if (item.Triggered(s)) {
                    trigger = item;
                    return true;
                }
            }
            trigger = null;
            return false;
        }

        bool ICommandTrigger.Triggered(IGesture g, out IGestureTrigger trigger) {
            foreach (var gesture in Gestures) {
                if (gesture?.Gesture?.Equals(g) == true) {
                    trigger = gesture;
                    return true;
                }
            }
            trigger = null;
            return false;
        }

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
            return new CommandTrigger(cmd.Inputs(), cmd.Gestures());
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
                private static GestureTriggerCollection Triggers<TGesture>(string input, Dictionary<string, string> set) where TGesture : IGesture, new() {
                    if (null == set) throw new ArgumentNullException(nameof(set));
                    return GestureTriggerCollection.From(set
                        .SelectMany(pair => pair.Key
                            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(key => (key, shortcut: pair.Value)))
                        .ToDictionary(
                            pair => new TGesture().Parse(pair.key),
                            pair => pair.shortcut.Replace(
                                "*",
                                input?.Split('|')?.FirstOrDefault()?.Trim() ?? "*")));
                }

                public string Input { get; set; }

                public Dictionary<string, string> Press {
                    get => _Press ??= new();
                    set => _Press = value;
                }
                private Dictionary<string, string> _Press;

                public Dictionary<string, string> Click {
                    get => _Click ??= new();
                    set => _Click = value;
                }
                private Dictionary<string, string> _Click;

                public Dictionary<string, string> Macro {
                    get => _Macro ??= new();
                    set => _Macro = value;
                }
                private Dictionary<string, string> _Macro;

                public InputTriggerCollection Inputs() {
                    var inputInput = Input?.Trim() ?? "";
                    var inputParts = inputInput.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var inputTrigger = inputParts.Length == 0
                        ? null
                        : new InputTrigger(null, inputParts[0], inputParts.Skip(1).ToArray());
                    var macro = Macro;
                    var macroTriggers = macro.Select(m => {
                        var macroInput = m.Key?.Trim() ?? "";
                        var macroParts = macroInput.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (macroParts.Length == 0) {
                            return null;
                        }
                        var macroDefined = m.Value.Replace("*", inputParts.Length == 0 ? "*" : inputParts[0]);
                        var macroTrigger = new InputTrigger(macroDefined, macroParts[0], macroParts.Skip(1).ToArray());
                        return macroTrigger;
                    });
                    return new InputTriggerCollection(macroTriggers
                        .Prepend(inputTrigger)
                        .Where(trigger => trigger != null));
                }

                public GestureTriggerCollection Gestures() {
                    var press = Triggers<PressGesture>(Input, Press);
                    var click = Triggers<ClickGesture>(Input, Click);
                    return GestureTriggerCollection.Combine(press, click);
                }
            }
        }
    }
}
