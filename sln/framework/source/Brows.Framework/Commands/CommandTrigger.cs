using Brows.Conf;
using Brows.Localization;
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

    private static string[] InputParts(string input) {
        return (input?.Trim() ?? "").Split(
            '|',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /*
     * The input of the qualified layer comes first, so that it is the main input of the command,
     * and the aliases of the unqualified layer follow it.
     */
    private static string MergeInputs(IReadOnlyList<Layer> layers) {
        var parts = new List<string>();
        for (var i = layers.Count - 1; i >= 0; i--) {
            foreach (var part in InputParts(layers[i].Input)) {
                var partIsNew = parts.Contains(part, StringComparer.OrdinalIgnoreCase) == false;
                if (partIsNew) {
                    parts.Add(part);
                }
            }
        }
        return parts.Count == 0 ? null : string.Join("|", parts);
    }

    private static Dictionary<string, string> MergeMacros(IReadOnlyList<Layer> layers) {
        var macro = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in layers) {
            foreach (var pair in layer.Macro ?? throw new ArgumentNullException(nameof(Layer.Macro))) {
                macro[pair.Key] = pair.Value;
            }
        }
        return macro;
    }

    private static InputTriggerCollection CompileInputs(string input, Dictionary<string, string> macro) {
        var inputParts = InputParts(input);
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

    /*
     * The sets are applied in order, so that a later set replaces or disables a gesture of an
     * earlier one. Within one set, a disabled gesture stays disabled whatever the order of its
     * entries.
     */
    private static GestureTriggerCollection CompileGestures<TGesture>(
        string input,
        IEnumerable<Dictionary<string, string>> sets,
        ITranslation translation)
    where TGesture : IGesture, new() {
        var gestures = new Dictionary<IGesture, string>();
        var inputAlias = input?.Split('|')?.FirstOrDefault()?.Trim() ?? "*";
        foreach (var set in sets) {
            if (set is null) {
                throw new ArgumentNullException(nameof(sets));
            }
            var setGestures = new Dictionary<IGesture, string>();
            var setDisabled = new HashSet<IGesture>();
            foreach (var pair in set) {
                var shortcutIsDisabled = string.Equals(
                    pair.Value?.Trim(),
                    "disabled",
                    StringComparison.OrdinalIgnoreCase);
                foreach (var key in pair.Key.Split(
                    '|',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                    var gesture = new TGesture().Parse(key, translation);
                    if (shortcutIsDisabled) {
                        setDisabled.Add(gesture);
                        continue;
                    }
                    setGestures[gesture] = pair.Value.Replace("*", inputAlias);
                }
            }
            foreach (var pair in setGestures) {
                if (setDisabled.Contains(pair.Key)) {
                    continue;
                }
                gestures[pair.Key] = pair.Value;
            }
            foreach (var gesture in setDisabled) {
                gestures.Remove(gesture);
            }
        }
        return GestureTriggerCollection.From(gestures, translation);
    }

    private static CommandTrigger Compile(IReadOnlyList<Layer> layers, ITranslation translation) {
        var input = MergeInputs(layers);
        var inputTriggers = CompileInputs(input, MergeMacros(layers));
        var pressTriggers = CompileGestures<PressGesture>(
            input,
            layers.Select(layer => layer.Press),
            translation);
        var clickTriggers = CompileGestures<ClickGesture>(
            input,
            layers.Select(layer => layer.Click),
            translation);
        var gestureTriggers = GestureTriggerCollection.Combine(pressTriggers, clickTriggers);
        return new CommandTrigger(inputTriggers, gestureTriggers);
    }

    internal static CommandTrigger Compile(
        string input,
        Dictionary<string, string> press,
        Dictionary<string, string> click,
        Dictionary<string, string> macro,
        ITranslation translation) {
        return Compile([new Layer(input, press, click, macro)], translation);
    }

    /*
     * The entry of a command's type name, such as cmd[Open], holds the triggers that every
     * command of that name shares across providers. The entry of the command's own name, such
     * as cmd[MBox_Open], is layered on top of it.
     */
    internal static CommandTrigger Resolve(
        string name,
        string typeName,
        Func<string, Layer> find,
        ITranslation translation) {
        ArgumentNullException.ThrowIfNull(find);
        var layers = new List<Layer>();
        var nameIsQualified = string.Equals(name, typeName, StringComparison.OrdinalIgnoreCase) == false;
        if (nameIsQualified) {
            var unqualified = find(typeName);
            if (unqualified is not null) {
                layers.Add(unqualified);
            }
        }
        var qualified = find(name);
        if (qualified is not null) {
            layers.Add(qualified);
        }
        if (layers.Count == 0) {
            return null;
        }
        return Compile(layers, translation);
    }

    internal sealed record Layer(
        string Input,
        Dictionary<string, string> Press,
        Dictionary<string, string> Click,
        Dictionary<string, string> Macro);

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
        return Resolve(
            name,
            command.GetType().Name,
            key => confCmd.TryGetValue(key, out var cmd)
                ? new Layer(cmd.Input, cmd.Press, cmd.Click, cmd.Macro)
                : null,
            command.Translate);
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
