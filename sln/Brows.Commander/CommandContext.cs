using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class CommandContext : Notifier, ICommandContext {
        private static readonly IReadOnlyDictionary<IInputTrigger, ICommand> EmptyStringCommands = new Dictionary<IInputTrigger, ICommand>();
        private static readonly IReadOnlyDictionary<IGestureTrigger, ICommand> EmptyGestureCommands = new Dictionary<IGestureTrigger, ICommand>();

        private IReadOnlySet<ICommand> TriggeredCommands => _TriggeredCommands ??=
            TriggeredCommand.Values.ToHashSet();
        private IReadOnlySet<ICommand> _TriggeredCommands;

        private string Input => _Input ??=
            Line?.HasInput(out var input) == true
                ? input.Trim()
                : "";
        private string _Input;

        private CommandContext(Commander commander, CommandSource source, CommandLine line, ICommandPalette palette, IGesture gesture) {
            Commander = commander ?? throw new ArgumentNullException(nameof(commander));
            Line = line;
            Source = source;
            Gesture = gesture;
            Palette = palette;
        }

        private Dictionary<ITrigger, ICommand> GetTriggeredCommands() {
            var set = new Dictionary<ITrigger, ICommand>();
            var commands = Commander.Commands;
            if (commands != null) {
                var line = Line;
                if (line != null && line.HasCommand(out var command) && commands.Triggered(command, out var triggeredByLine)) {
                    foreach (var triggered in triggeredByLine) {
                        set[triggered.Key] = triggered.Value;
                    }
                }
                var gesture = Gesture;
                if (gesture != null && commands.Triggered(gesture, out var triggeredByGesture)) {
                    foreach (var triggered in triggeredByGesture) {
                        set[triggered.Key] = triggered.Value;
                    }
                }
            }
            return set;
        }

        public IReadOnlyDictionary<ITrigger, ICommand> TriggeredCommand => _TriggeredCommand ??=
            GetTriggeredCommands();
        private IReadOnlyDictionary<ITrigger, ICommand> _TriggeredCommand;

        public Commander Commander { get; }
        public CommandLine Line { get; }
        public CommandSource Source { get; }
        public IGesture Gesture { get; }
        public ICommandPalette Palette { get; }

        public ICommandContextFlag Flag {
            get => _Flag;
            private set => Change(ref _Flag, value, nameof(Flag));
        }
        private ICommandContextFlag _Flag;

        public CommandContext(Commander commander, CommandSource source, CommandLine line, ICommandPalette palette) : this(commander, source, line, palette, gesture: null) {
        }

        public CommandContext(Commander commander, CommandSource source, CommandLine line, IGesture gesture) : this(commander, source, line, palette: null, gesture) {
        }

        public bool DidTrigger(out IReadOnlySet<ICommand> commands) {
            commands = TriggeredCommands;
            return commands.Count > 0 && (Line?.HasTrigger(out _) ?? true);
        }

        public bool MayTrigger(ICommand command) {
            return TriggeredCommands.Contains(command);
        }

        public bool DidTrigger(ICommand command) {
            return MayTrigger(command) && (Line?.HasTrigger(out _) ?? true);
        }

        public void SetConf(ICommandContextConf conf) {
            var palette = Commander?.Palette;
            if (palette != null) {
                palette.SuggestionConf = conf;
            }
        }

        public bool HasConf(out ICommandContextConf conf) {
            var text = Commander?.Palette?.Input?.Conf?.Text;
            if (text == null) {
                conf = null;
                return false;
            }
            var command = Commander?.Palette?.Input?.Conf?.Suggestion?.Command;
            conf = new CommandContextConf(command, text);
            return true;
        }

        public void SetData(ICommandContextData data) {
            var palette = Commander?.Palette;
            if (palette != null) {
                palette.SuggestionData = data;
            }
        }

        public bool HasData(out ICommandContextData data) {
            data = Commander?.Palette?.SuggestionData;
            return data != null;
        }

        public void SetFlag(ICommandContextFlag flag) {
            Flag = flag;
        }

        public bool HasFlag(out ICommandContextFlag flag) {
            flag = Flag;
            return flag != null;
        }

        public bool HasPalette(out ICommandPalette palette) {
            palette = Palette;
            return palette != null;
        }

        public bool HasCommander(out ICommander commander) {
            commander = Commander;
            return commander != null;
        }

        public bool HasDomain(out ICommanderDomain domain) {
            domain = Commander?.Domain;
            return domain != null;
        }

        public bool HasLine(out ICommandLine line) {
            line = Line;
            return line != null;
        }

        public bool HasInput(out string value) {
            value = Input;
            return value != "";
        }

        public bool HasSource(out ICommandSource source) {
            source = Source;
            return source != null;
        }

        public bool HasSource<T>(out T item, out IReadOnlyList<T> items) {
            if (Source?.Item is not T t) {
                item = default;
                items = default;
                return false;
            }
            item = t;
            items = Source?.Items?.OfType<T>()?.ToList();
            return items?.Count > 0;
        }

        public bool HasPanel(out IPanel active) {
            active = Commander?.Panels?.Active;
            return active != null;
        }

        public bool HasPanel(int column, out IPanel panel) {
            if (HasPanels(out var panels)) {
                if (panels.HasColumn(column, out panel)) {
                    return true;
                }
            }
            panel = null;
            return false;
        }

        public bool HasPanels(out IPanelCollection collection) {
            collection = Commander?.Panels;
            return collection != null && collection.Count > 0;
        }

        public bool HasPanels(PanelPassiveMode mode, out IPanel active, out IPanel passive) {
            if (HasPanel(out active) == false) {
                passive = null;
                return false;
            }
            var column = active.Column;
            switch (mode) {
                case PanelPassiveMode.None:
                    column = -1;
                    break;
                case PanelPassiveMode.Auto:
                    if (HasPanels(out var collection) == false) {
                        passive = null;
                        return false;
                    }
                    switch (collection.Count) {
                        case 1:
                            break;
                        case 2:
                            column = active.Column == 0
                                ? 1
                                : active.Column == 1
                                    ? 0
                                    : -1;
                            break;
                        default:
                            column = -1;
                            break;
                    }
                    break;
                case PanelPassiveMode.Next:
                    column++;
                    break;
                case PanelPassiveMode.Previous:
                    column--;
                    break;
            }
            return HasPanel(column, out passive);
        }

        public bool HasGesture(out IGesture gesture) {
            if (Gesture != null) {
                gesture = Gesture;
                return true;
            }
            gesture = null;
            return false;
        }

        public void SetHint(ICommandContextHint hint) {
            var palette = Commander?.Palette;
            if (palette != null) {
                palette.SuggestionHint = hint;
            }
        }

        public bool Operate(Func<IOperationProgress, CancellationToken, Task<bool>> task) {
            var @operator = Commander?.Operator;
            if (@operator == null) {
                return false;
            }
            var name = HasInput(out var input)
                ? input
                : HasGesture(out var gesture)
                    ? gesture.Display()
                    : nameof(Operate);
            @operator.Operate(name, async (operationProgress, cancellationToken) => {
                var w = false;
                var t = task?.Invoke(operationProgress, cancellationToken);
                if (t != null) {
                    w = await t;
                }
            });
            return true;
        }

        public bool ShowPalette(ICommandPaletteConfig config) {
            return Operate(async (progress, token) => {
                return await Commander.ShowPalette(Source, config, token);
            });

        }
    }
}
