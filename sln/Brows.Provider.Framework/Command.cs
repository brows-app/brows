using Domore.Conf.Cli;
using Domore.Conf.Extensions;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class Command : ICommand {
        private readonly CommandHistory History = new();

        private bool Valid(ICommandContext context) {
            if (Provided(context) == false) {
                return false;
            }
            if (Sourced(context) == false) {
                return false;
            }
            return true;
        }

        private bool Provided(ICommandContext context) {
            var provider = Provider;
            if (provider == null) {
                return true;
            }
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) {
                return false;
            }
            if (active.HasProvider(out Provider activeProvider) == false) {
                return false;
            }
            var activeProviderType = activeProvider.GetType();
            if (activeProviderType != provider) {
                return false;
            }
            return true;
        }

        private bool Sourced(ICommandContext context) {
            var source = Source;
            if (source == null) {
                return true;
            }
            if (context == null) return false;
            if (context.HasSource(out object item, out var items) == false) {
                return false;
            }
            if (source.Any(type => type.IsAssignableFrom(item.GetType())) == false) {
                return false;
            }
            if (source.Any(type => items.All(item => type.IsAssignableFrom(item.GetType()))) == false) {
                return false;
            }
            return true;
        }

        internal string Name {
            get {
                if (_Name == null) {
                    var type = GetType();
                    var name = type.Name;
                    var provider = Provider;
                    if (provider != null) {
                        var providerName = provider.Name;
                        if (providerName.EndsWith("provider", StringComparison.OrdinalIgnoreCase)) {
                            providerName = providerName.Substring(0, providerName.Length - "provider".Length);
                        }
                        name = $"{providerName}_{name}";
                    }
                    _Name = name;
                }
                return _Name;
            }
        }
        private string _Name;

        protected CommandSuggestionRelevance SuggestionRelevance =>
            _SuggestionRelevance ?? (
            _SuggestionRelevance = new CommandSuggestionRelevance());
        private CommandSuggestionRelevance _SuggestionRelevance;

        protected string InputTrigger =>
            Trigger?.Input?.String;

        protected virtual Type Provider =>
            null;

        protected virtual IEnumerable<Type> Source =>
            null;

        protected ICommandSuggestion Suggestion(ICommandContext context, string input, string help = null, string group = null, int? relevance = null, string description = null, string press = null, string alias = null, bool? history = null) {
            return new CommandSuggestion {
                Description = description ?? CommandTranslation.Description(Name),
                Group = CommandTranslation.Group(group),
                Help = help ?? input,
                History = history ?? false,
                Input = input,
                Alias = alias ?? string.Join(",", Trigger?.Input?.Aliases?.AsEnumerable() ?? Array.Empty<string>()),
                Press = press ?? string.Join(",", Trigger?.Gesture?.Select(p => p.Display) ?? Array.Empty<string>()),
                Relevance = relevance ?? 0
            };
        }

        protected virtual bool TriggeredWork(ICommandContext context) {
            return false;
        }

        protected virtual bool ArbitraryWork(ICommandContext context) {
            return false;
        }

        protected virtual Task Init(CancellationToken token) {
            return Task.CompletedTask;
        }

        protected virtual async IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, [EnumeratorCancellation] CancellationToken token) {
            if (context == null) yield break;
            if (context.DidTrigger(out var triggered)) {
                if (triggered.Contains(this) == false) {
                    yield break;
                }
            }
            if (context.HasLine(out var line)) {
                var command = line.HasCommand(out var c) ? c : "";
                var trigger = Trigger?.Input;
                if (trigger != null) {
                    var relevance = SuggestionRelevance.From(trigger.Options, command);
                    if (relevance.HasValue) {
                        yield return Suggestion(
                            context: context,
                            help: trigger.String + " " + HelpLine,
                            input: trigger.String,
                            relevance: relevance.Value);
                        foreach (var item in History.AsEnumerable()) {
                            if (item.HasInput(out var input)) {
                                yield return Suggestion(
                                    context: context,
                                    help: input,
                                    history: true,
                                    input: input,
                                    description: "",
                                    alias: "",
                                    press: "",
                                    relevance: relevance.Value,
                                    group: "CommandHistory");
                            }
                        }
                    }
                }
            }
            await Task.CompletedTask;
        }

        public virtual string HelpLine => CommandTranslation.Help(Name);
        public virtual string ConfText => CommandTranslation.Conf(this);

        public ICommandTrigger Trigger { get; private set; }

        bool ICommand.TriggeredWork(ICommandContext context) {
            if (Valid(context) == false) {
                return false;
            }
            var worked = TriggeredWork(context);
            if (worked) {
                if (context.HasLine(out var line)) {
                    if (line.HasParameter(out _)) {
                        History.Add(line);
                    }
                }
            }
            return worked;
        }

        bool ICommand.ArbitraryWork(ICommandContext context) {
            if (Valid(context) == false) {
                return false;
            }
            return ArbitraryWork(context);
        }

        IAsyncEnumerable<ICommandSuggestion> ICommand.Suggest(ICommandContext context, CancellationToken token) {
            if (Valid(context) == false) {
                return CommandSuggestions.Empty();
            }
            return Suggest(context, token);
        }

        async Task ICommand.Init(CancellationToken token) {
            Trigger = await CommandTrigger.For(this, token);
            await Init(token);
        }
    }

    public abstract class Command<TParameter> : Command where TParameter : new() {
        private Context Proxy(ICommandContext context) {
            return new Context(context, this);
        }

        public override string HelpLine =>
            _HelpLine ?? (
            _HelpLine = Cli.Display(new TParameter()).Split([' '], 2).LastOrDefault());
        private string _HelpLine;

        protected virtual TParameter ParameterFactory(string parameter, string conf) {
            var
            inst = new TParameter();
            inst = inst.ConfFrom(conf, key: "");
            inst = Cli.Configure(inst, parameter);
            return inst;
        }

        protected virtual bool Work(Context context) =>
            base.TriggeredWork(context);

        protected virtual IAsyncEnumerable<ICommandSuggestion> Suggest(Context context, CancellationToken token) {
            if (context != null) {
                if (context.DidTrigger(this)) {
                    var confText = ConfText;
                    if (confText != null) {
                        if (context.HasConf(out var conf) == false || conf.Command != this) {
                            context.SetConf(new CommandContextConf(this, confText));
                        }
                        return CommandSuggestions.Empty();
                    }
                }
            }
            return base.Suggest(context, token);
        }

        protected sealed override bool ArbitraryWork(ICommandContext context) =>
            base.ArbitraryWork(context);

        protected sealed override bool TriggeredWork(ICommandContext context) =>
            Work(Proxy(context));

        protected sealed override IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, CancellationToken token) =>
            Suggest(Proxy(context), token);

        protected class Context : ICommandContext {
            private static readonly ILog Log = Logging.For(typeof(Context));

            private TParameter Parameter;
            private bool ParameterParsed;
            private bool ParameterExists;
            private Exception ParameterException;

            internal ICommandContext Agent { get; }
            internal Command<TParameter> Command { get; }

            internal Context(ICommandContext agent, Command<TParameter> command) {
                Agent = agent ?? throw new ArgumentNullException(nameof(agent));
                Command = command ?? throw new ArgumentNullException(nameof(command));
            }

            public bool HasParameter(out TParameter parameter) {
                if (ParameterParsed == false) {
                    ParameterParsed = true;
                    if (Agent.HasLine(out var line)) {
                        if (line.HasParameter(out var p) | line.HasConf(out var c)) {
                            try {
                                Parameter = Command.ParameterFactory(p, c);
                                ParameterExists = true;
                            }
                            catch (Exception ex) {
                                Parameter = default;
                                ParameterExists = false;
                                ParameterException = ex;
                            }
                        }
                    }
                }
                parameter = Parameter;
                return ParameterExists;
            }

            public bool HasParameterError(out Exception error) {
                if (HasParameter(out _)) {
                    error = null;
                    return false;
                }
                error = ParameterException;
                return error != null;
            }

            public bool GetParameter(out TParameter parameter) {
                if (HasParameter(out parameter)) {
                    return true;
                }
                if (HasParameterError(out _)) {
                    return false;
                }
                parameter = new TParameter();
                return true;
            }

            public void SetConf(ICommandContextConf conf) {
                Agent.SetConf(conf);
            }

            public bool HasConf(out ICommandContextConf conf) {
                return Agent.HasConf(out conf);
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

            public bool HasPalette(out ICommandPalette palette) {
                return Agent.HasPalette(out palette);
            }

            public bool HasCommander(out ICommander commander) {
                return Agent.HasCommander(out commander);
            }

            public bool HasDomain(out ICommanderDomain domain) {
                return Agent.HasDomain(out domain);
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

            public bool HasPanels(out IPanelCollection collection) {
                return Agent.HasPanels(out collection);
            }

            public bool HasPanels(PanelPassiveMode mode, out IPanel active, out IPanel passive) {
                return Agent.HasPanels(mode, out active, out passive);
            }

            public bool HasGesture(out IGesture gesture) {
                return Agent.HasGesture(out gesture);
            }

            public bool HasLine(out ICommandLine line) {
                return Agent.HasLine(out line);
            }

            public bool MayTrigger(ICommand command) {
                return Agent.MayTrigger(command);
            }

            public bool DidTrigger(ICommand command) {
                return Agent.DidTrigger(command);
            }

            public bool DidTrigger(out IReadOnlySet<ICommand> commands) {
                return Agent.DidTrigger(out commands);
            }

            public bool Operate(Func<IOperationProgress, CancellationToken, Task<bool>> task) {
                return Agent.Operate(task);
            }

            public void SetHint(ICommandContextHint hint) {
                Agent.SetHint(hint);
            }

            public bool HasSource<T>(out T item, out IReadOnlyList<T> items) {
                return Agent.HasSource(out item, out items);
            }

            public bool HasSource(out ICommandSource source) {
                return Agent.HasSource(out source);
            }

            public bool ShowPalette(string input) {
                return Agent.ShowPalette(input);
            }

            public bool ShowPalette(string input, int selectedStart, int selectedLength) {
                return Agent.ShowPalette(input, selectedStart, selectedLength);
            }
        }
    }
}
