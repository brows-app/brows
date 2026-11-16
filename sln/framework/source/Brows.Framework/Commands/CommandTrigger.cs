using Brows.Conf;
using Brows.Triggers;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class CommandTrigger : ICommandTrigger {
    private static readonly ILog Log = Logging.For(typeof(CommandTrigger));
    private static readonly SemaphoreSlim Locker = new(1, 1);
    private static readonly Lock ConfigLocker = new();

    private static IConfiguration<Trigger> Config {
        get {
            lock (ConfigLocker) {
                if (field is null) {
                    var configFile = Imports.Current?.Find<IConfigurationFile>(
                        throwIfNotFound: false,
                        throwIfNotReady: false) ?? new ConfigurationFile();
                    field = configFile.File<Trigger>();
                    field.Changed += Config_Changed;
                }
                return field;
            }
        }
    }

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

    private static InputTriggerCollection CompileInputs(string input, Dictionary<string, string> macro) {
        var inputInput = input?.Trim() ?? "";
        var inputParts = inputInput.Split(
            '|',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var inputTrigger = inputParts.Length == 0
            ? null
            : new InputTrigger(null, inputParts[0], inputParts.Skip(1).ToArray());
        var macroTriggers = macro.Select(item => {
            var macroInput = item.Key?.Trim() ?? "";
            var macroParts = macroInput.Split(
                '|',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (macroParts.Length == 0) {
                return null;
            }
            var macroDefined = item.Value.Replace("*", inputParts.Length == 0 ? "*" : inputParts[0]);
            return new InputTrigger(macroDefined, macroParts[0], macroParts.Skip(1).ToArray());
        });
        return new InputTriggerCollection(macroTriggers
            .Prepend(inputTrigger)
            .Where(trigger => trigger is not null));
    }

    private static GestureTriggerCollection CompileGestures<TGesture>(
        string input,
        Dictionary<string, string> set)
    where TGesture : IGesture, new() {
        if (set is null) {
            throw new ArgumentNullException(nameof(set));
        }

        var gestures = new Dictionary<IGesture, string>();
        var disabledGestures = new HashSet<IGesture>();
        var inputAlias = input?.Split('|')?.FirstOrDefault()?.Trim() ?? "*";
        foreach (var pair in set) {
            var shortcutIsDisabled = string.Equals(
                pair.Value?.Trim(),
                "disabled",
                StringComparison.OrdinalIgnoreCase);
            foreach (var key in pair.Key.Split(
                '|',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                var gesture = new TGesture().Parse(key);
                if (shortcutIsDisabled) {
                    disabledGestures.Add(gesture);
                    gestures.Remove(gesture);
                    continue;
                }
                if (disabledGestures.Contains(gesture)) {
                    continue;
                }
                gestures[gesture] = pair.Value.Replace("*", inputAlias);
            }
        }

        foreach (var gesture in disabledGestures) {
            gestures.Remove(gesture);
        }
        return GestureTriggerCollection.From(gestures);
    }

    internal static CommandTrigger Compile(
        string input,
        Dictionary<string, string> press,
        Dictionary<string, string> click,
        Dictionary<string, string> macro) {
        var inputTriggers = CompileInputs(input, macro);
        var pressTriggers = CompileGestures<PressGesture>(input, press);
        var clickTriggers = CompileGestures<ClickGesture>(input, click);
        var gestureTriggers = GestureTriggerCollection.Combine(pressTriggers, clickTriggers);
        return new CommandTrigger(inputTriggers, gestureTriggers);
    }

    public static event EventHandler Changed;

    public InputTriggerCollection Inputs { get; }
    public GestureTriggerCollection Gestures { get; }

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
        return Compile(cmd.Input, cmd.Press, cmd.Click, cmd.Macro);
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

        public sealed class Command {
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
        }
    }

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
}
