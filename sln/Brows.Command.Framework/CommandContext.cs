using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    using Cli;
    using Triggers;

    public class CommandContext : Notifier, ICommandContext {
        private static readonly IReadOnlySet<ICommand> EmptyCommands = new HashSet<ICommand>(0);

        private IReadOnlySet<ICommand> TriggeredCommands;

        private string Input =>
            _Input ?? (
            _Input = Info?.Input?.Trim() ?? "");
        private string _Input;

        public ITrigger Trigger { get; }
        public ICommander Commander { get; }
        public ICommandInfo Info { get; }

        public ICommandContextFlag Flag {
            get => _Flag;
            private set => Change(ref _Flag, value, nameof(Flag));
        }
        private ICommandContextFlag _Flag;

        public CommandContext(ICommander commander, ITrigger trigger, ICommandInfo info = null) {
            Commander = commander ?? throw new ArgumentNullException(nameof(commander));
            Trigger = trigger;
            Info = info;
        }

        public bool HasTriggered(out IReadOnlySet<ICommand> commands) {
            if (TriggeredCommands == null) {
                if (Commander.Commands.Triggered(Trigger, out var triggeredCommands)) {
                    TriggeredCommands = triggeredCommands;
                }
                if (TriggeredCommands == null) {
                    TriggeredCommands = EmptyCommands;
                }
            }
            if (TriggeredCommands.Count > 0) {
                commands = TriggeredCommands;
                return true;
            }
            commands = null;
            return false;
        }

        public bool DidTrigger(ICommand command) {
            if (HasTriggered(out var commands)) {
                return commands.Contains(command);
            }
            return false;
        }

        public virtual void SetData(ICommandContextData data) {
            var palette = Commander?.Palette;
            if (palette != null) {
                palette.SuggestionData = data;
            }
        }

        public bool HasData(out ICommandContextData data) {
            data = Commander?.Palette?.SuggestionData;
            return data != null;
        }

        public virtual void SetFlag(ICommandContextFlag flag) {
            Flag = flag;
        }

        public virtual bool HasFlag(out ICommandContextFlag flag) {
            flag = Flag;
            return flag != null;
        }

        public bool HasTrigger(out ITrigger trigger) {
            trigger = Trigger;
            return trigger != null;
        }

        public bool HasClipboard(out IClipboard clipboard) {
            clipboard = Commander?.Clipboard;
            return clipboard != null;
        }

        public bool CanBookmark(out IBookmark bookmark) {
            bookmark = Commander?.Panels?.Active?.Provider?.Bookmark;
            return bookmark != null;
        }

        public bool HasCommander(out ICommander commander) {
            commander = Commander;
            return commander != null;
        }

        public bool HasInfo(out ICommandInfo info) {
            info = Info;
            return info != null;
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
            panel = Commander?.Panels?.FirstOrDefault(p => p.Column == column);
            return panel != null;
        }

        public bool HasPanels(out IReadOnlyList<IPanel> collection) {
            collection = Commander?.Panels;
            return collection != null && collection.Count > 0;
        }

        public bool HasPanels(PanelPassiveMode mode, out IPanel active, out IPanel passive) {
            if (HasPanel(out active)) {
                var column = active.Column;
                switch (mode) {
                    case PanelPassiveMode.Next:
                        column++;
                        break;
                    case PanelPassiveMode.Previous:
                        column--;
                        break;
                }
                return HasPanel(column, out passive);
            }
            passive = null;
            return false;
        }

        public bool HasProvider(out IPanelProvider provider) {
            if (HasPanel(out var panel)) {
                provider = panel.Provider;
                return provider != null;
            }
            provider = null;
            return false;
        }

        public bool HasKey(out KeyboardGesture gesture) {
            if (Trigger is KeyboardTrigger kt) {
                gesture = kt.Gesture;
                return true;
            }
            gesture = default(KeyboardGesture);
            return false;
        }

        public void SetHint(ICommandContextHint hint) {
            var palette = Commander?.Palette;
            if (palette != null) {
                palette.SuggestionHint = hint;
            }
        }
    }

    public abstract class CommandContext<TParameter> : ICommandContext {
        private TParameter Parameter;
        private bool ParameterParsed;
        private bool ParameterExists;

        protected abstract TParameter Factory();

        public ICommandContext Agent { get; }
        public ICommandParser Parser { get; }

        public CommandContext(ICommandContext agent, ICommandParser parser) {
            Agent = agent ?? throw new ArgumentNullException(nameof(agent));
            Parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public bool HasParameter(out TParameter parameter) {
            if (ParameterParsed == false) {
                ParameterParsed = true;
                if (Agent.HasInfo(out var info)) {
                    var p = info.Parameter?.Trim() ?? "";
                    if (p != "") {
                        Parameter = Factory();
                        try {
                            Parser.Parse(p, Parameter);
                            ParameterExists = true;
                        }
                        catch {
                            Parameter = default(TParameter);
                            ParameterExists = false;
                        }
                    }
                }
            }
            parameter = Parameter;
            return ParameterExists;
        }

        public void SetData(ICommandContextData data) {
            Agent.SetData(data);
        }

        public bool HasData(out ICommandContextData data) {
            return Agent.HasData(out data);
        }

        public void SetFlag(ICommandContextFlag flag) {
            Agent.SetFlag(flag);
        }

        public bool HasFlag(out ICommandContextFlag flag) {
            return Agent.HasFlag(out flag);
        }

        public bool CanBookmark(out IBookmark bookmark) {
            return Agent.CanBookmark(out bookmark);
        }

        public bool HasClipboard(out IClipboard clipboard) {
            return Agent.HasClipboard(out clipboard);
        }

        public bool HasCommander(out ICommander commander) {
            return Agent.HasCommander(out commander);
        }

        public bool HasInput(out string value) {
            return Agent.HasInput(out value);
        }

        public bool HasPanel(out IPanel active) {
            return Agent.HasPanel(out active);
        }

        public bool HasPanel(int column, out IPanel panel) {
            return Agent.HasPanel(column, out panel);
        }

        public bool HasPanels(out IReadOnlyList<IPanel> collection) {
            return Agent.HasPanels(out collection);
        }

        public bool HasPanels(PanelPassiveMode mode, out IPanel active, out IPanel passive) {
            return Agent.HasPanels(mode, out active, out passive);
        }

        public bool HasKey(out KeyboardGesture gesture) {
            return Agent.HasKey(out gesture);
        }

        public bool HasProvider(out IPanelProvider provider) {
            return Agent.HasProvider(out provider);
        }

        public bool HasInfo(out ICommandInfo info) {
            return Agent.HasInfo(out info);
        }

        public bool HasTrigger(out ITrigger trigger) {
            return Agent.HasTrigger(out trigger);
        }

        public bool DidTrigger(ICommand command) {
            return Agent.DidTrigger(command);
        }

        public void SetHint(ICommandContextHint hint) {
            Agent.SetHint(hint);
        }
    }
}
