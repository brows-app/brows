using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    using Cli;

    public class CommandContext : Notifier, ICommandContext {
        private static readonly IReadOnlySet<ICommand> EmptyCommands = new HashSet<ICommand>(0);

        private string Input =>
            _Input ?? (
            _Input = Line?.HasInput(out var input) == true
                ? input.Trim()
                : "");
        private string _Input;

        private CommandContext(ICommander commander, ICommandLine line = null, PressGesture? press = null) {
            Commander = commander ?? throw new ArgumentNullException(nameof(commander));
            Line = line;
            Press = press;
        }

        public IReadOnlySet<ICommand> TriggeringCommands =>
            _TriggeringCommands ?? (
            _TriggeringCommands =
                Line != null && Line.HasCommand(out var command) && Commander.Commands.Triggered(command, out var lineCommands)
                ? lineCommands
                : Press.HasValue && Commander.Commands.Triggered(Press.Value, out var pressCommands)
                    ? pressCommands
                    : EmptyCommands);
        private IReadOnlySet<ICommand> _TriggeringCommands;

        public ICommander Commander { get; }
        public ICommandLine Line { get; }
        public PressGesture? Press { get; }

        public ICommandContextFlag Flag {
            get => _Flag;
            private set => Change(ref _Flag, value, nameof(Flag));
        }
        private ICommandContextFlag _Flag;

        public CommandContext(ICommander commander, ICommandLine line) : this(commander, line: line, press: null) {
        }

        public CommandContext(ICommander commander, ICommandLine line, PressGesture press) : this(commander, line, press: (PressGesture?)press) {
        }

        public bool MayTrigger(ICommand command) {
            return TriggeringCommands.Contains(command);
        }

        public bool DidTrigger(ICommand command) {
            return
                MayTrigger(command) && (
                Line?.HasTrigger(out _) ?? true);
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

        public bool HasLine(out ICommandLine line) {
            line = Line;
            return line != null;
        }

        public bool HasInput(out string value) {
            value = Input;
            return value != "";
        }

        public bool HasEntries(out IEntryCollection entries) {
            entries = Commander?.Panels?.Active?.Entries;
            return entries != null;
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

        public bool HasKey(out PressGesture gesture) {
            if (Press.HasValue) {
                gesture = Press.Value;
                return true;
            }
            gesture = default;
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
                if (Agent.HasLine(out var line)) {
                    if (line.HasParameter(out var p)) {
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

        public bool HasEntries(out IEntryCollection entries) {
            return Agent.HasEntries(out entries);
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

        public bool HasKey(out PressGesture gesture) {
            return Agent.HasKey(out gesture);
        }

        public bool HasProvider(out IPanelProvider provider) {
            return Agent.HasProvider(out provider);
        }

        public bool HasLine(out ICommandLine line) {
            return Agent.HasLine(out line);
        }

        public bool DidTrigger(ICommand command) {
            return Agent.DidTrigger(command);
        }

        public bool MayTrigger(ICommand command) {
            return Agent.MayTrigger(command);
        }

        public void SetHint(ICommandContextHint hint) {
            Agent.SetHint(hint);
        }
    }
}
