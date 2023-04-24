using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class CommandContext : Notifier, ICommandContext {
        private static readonly IReadOnlySet<ICommand> EmptyCommands = new HashSet<ICommand>(0);

        private string Input =>
            _Input ?? (
            _Input = Line?.HasInput(out var input) == true
                ? input.Trim()
                : "");
        private string _Input;

        public IReadOnlySet<ICommand> TriggeringCommands =>
            _TriggeringCommands ?? (
            _TriggeringCommands =
                Line != null && Line.HasCommand(out var command) && Commander.Commands.Triggered(command, out var lineCommands)
                    ? lineCommands
                    : Gesture != null && Commander.Commands.Triggered(Gesture, out var gestureCommands)
                        ? gestureCommands
                        : EmptyCommands);
        private IReadOnlySet<ICommand> _TriggeringCommands;

        public Commander Commander { get; }
        public CommandLine Line { get; }
        public IGesture Gesture { get; }

        public ICommandContextFlag Flag {
            get => _Flag;
            private set => Change(ref _Flag, value, nameof(Flag));
        }
        private ICommandContextFlag _Flag;

        public CommandContext(Commander commander, CommandLine line, IGesture gesture = null) {
            Commander = commander ?? throw new ArgumentNullException(nameof(commander));
            Line = line;
            Gesture = gesture;
        }

        public bool DidTrigger(out IReadOnlySet<ICommand> commands) {
            commands = TriggeringCommands;
            return commands.Count > 0 && (Line?.HasTrigger(out _) ?? true);
        }

        public bool MayTrigger(ICommand command) {
            return TriggeringCommands.Contains(command);
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
    }
}