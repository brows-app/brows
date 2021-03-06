using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Cli;
    using Translation;
    using Triggers;

    public abstract class Command : ICommand {
        private ITranslation Translate =>
            Global.Translation;

        private string SuggestionDescriptionKey =>
            _SuggestionDescriptionKey ?? (
            _SuggestionDescriptionKey = $"Command_Description_{GetType()?.Name}");
        private string _SuggestionDescriptionKey;

        private string SuggestionGroupKey =>
            _SuggestionGroupKey ?? (
            _SuggestionGroupKey = $"Command_Group_{nameof(Command)}");
        private string _SuggestionGroupKey;

        protected CommandSuggestionRelevance SuggestionRelevance =>
            _SuggestionRelevance ?? (
            _SuggestionRelevance = new CommandSuggestionRelevance());
        private CommandSuggestionRelevance _SuggestionRelevance;

        protected virtual IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield break;
            }
        }

        protected IEnumerable<ITrigger> _DefaultTriggers { get; }

        protected bool Triggered(string s) {
            return InputTriggers?.Any(trigger => trigger.Triggered(s)) ?? false;
        }

        protected bool Triggered(ICommandInfo info) {
            if (info == null) return false;
            return Triggered(info.Command);
        }

        protected bool Triggered(ICommandContext context) {
            if (context == null) return false;
            if (context.HasInfo(out var info)) {
                return Triggered(info);
            }
            return false;
        }

        protected ICommandSuggestion Suggestion(ICommandContext context, string input, int? relevance = null, string description = null, string group = null, string help = null) {
            return new CommandSuggestion(this, context) {
                Description = description == null
                    ? Translate.Value(SuggestionDescriptionKey)
                    : description,
                Group = group == null
                    ? Translate.Value(SuggestionGroupKey)
                    : Translate.Value($"Command_Group_{group}"),
                Help = help,
                Input = input,
                Relevance = relevance.HasValue
                    ? relevance.Value
                    : default(int)
            };
        }

        protected virtual bool ProtectedWorkable(ICommandContext context) {
            return true;
        }

        protected virtual Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            return Task.FromResult(false);
        }

        protected virtual async IAsyncEnumerable<ICommandSuggestion> ProtectedSuggestAsync(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (context == null) yield break;
            if (context.HasInfo(out var info)) {
                var command = info.Command;
                foreach (var trigger in InputTriggers) {
                    var relevance = SuggestionRelevance.From(trigger.Aggregate, command);
                    if (relevance.HasValue) {
                        yield return Suggestion(
                            context: context,
                            help: string.Join(" ", trigger.Value, HelpLine),
                            input: trigger.Value,
                            relevance: relevance.Value);
                    }
                    await Task.Yield();
                }
            }
        }

        protected string InputTrigger() {
            var inputTrigger = InputTriggers.FirstOrDefault();
            if (inputTrigger == null) throw new InvalidOperationException($"{nameof(InputTrigger)} [{inputTrigger}]");
            return inputTrigger.Value;
        }

        public virtual string Name => GetType().FullName;
        public virtual string HelpLine => null;
        public virtual bool Arbitrary => false;

        public IEnumerable<InputTrigger> InputTriggers =>
            _InputTriggers ?? (
            _InputTriggers = new HashSet<InputTrigger>(DefaultTriggers.OfType<InputTrigger>()));
        private IEnumerable<InputTrigger> _InputTriggers;

        public IEnumerable<KeyboardTrigger> KeyboardTriggers =>
            _KeyboardTriggers ?? (
            _KeyboardTriggers = new HashSet<KeyboardTrigger>(DefaultTriggers.OfType<KeyboardTrigger>()));
        private IEnumerable<KeyboardTrigger> _KeyboardTriggers;

        public bool Workable(ICommandContext context) {
            return ProtectedWorkable(context);
        }

        public Task<bool> WorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            return ProtectedWorkAsync(context, cancellationToken);
        }

        public IAsyncEnumerable<ICommandSuggestion> SuggestAsync(ICommandContext context, CancellationToken cancellationToken) {
            return ProtectedSuggestAsync(context, cancellationToken);
        }

        protected class ArgumentAttribute : CommandArgumentAttribute {
        }

        protected class SwitchAttribute : CommandSwitchAttribute {
        }
    }

    public abstract class Command<TParameter> : Command where TParameter : new() {
        private ICommandLine CommandLine {
            get => _CommandLine ?? (_CommandLine = new CommandLine());
            set => _CommandLine = value;
        }
        private ICommandLine _CommandLine;

        private ICommandHelp Help =>
            _Help ?? (
            _Help = CommandLine.Helper.Help(typeof(TParameter)));
        private ICommandHelp _Help;

        private Context NewContext(ICommandContext context) {
            return new Context(context, CommandLine.Parser);
        }

        protected virtual bool ProtectedWorkable(Context context) => base.ProtectedWorkable(context);
        protected virtual Task<bool> ProtectedWorkAsync(Context context, CancellationToken cancellationToken) => base.ProtectedWorkAsync(context, cancellationToken);
        protected virtual IAsyncEnumerable<ICommandSuggestion> ProtectedSuggestAsync(Context context, CancellationToken cancellationToken) => base.ProtectedSuggestAsync(context, cancellationToken);

        protected sealed override bool ProtectedWorkable(ICommandContext context) => ProtectedWorkable(NewContext(context));
        protected sealed override Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) => ProtectedWorkAsync(NewContext(context), cancellationToken);
        protected sealed override IAsyncEnumerable<ICommandSuggestion> ProtectedSuggestAsync(ICommandContext context, CancellationToken cancellationToken) => ProtectedSuggestAsync(NewContext(context), cancellationToken);

        public override string HelpLine => Help.HelpLine;

        protected class Context : CommandContext<TParameter> {
            protected override TParameter Factory() {
                return new TParameter();
            }

            public Context(ICommandContext agent, ICommandParser parser) : base(agent, parser) {
            }
        }
    }
}
