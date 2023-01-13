using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Cli;
    using Translation;

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

        protected static readonly Task<bool> Worked = Task.FromResult(true);

        protected CommandSuggestionRelevance SuggestionRelevance =>
            _SuggestionRelevance ?? (
            _SuggestionRelevance = new CommandSuggestionRelevance());
        private CommandSuggestionRelevance _SuggestionRelevance;

        protected bool Triggered(string s) {
            return Trigger?.Input?.Triggered(s) ?? false;
        }

        protected bool Triggered(ICommandLine line) {
            if (line == null) return false;
            if (line.HasTrigger(out var trigger)) {
                return Triggered(trigger);
            }
            return false;
        }

        protected bool Triggered(ICommandContext context) {
            if (context == null) return false;
            if (context.HasLine(out var line)) {
                return Triggered(line);
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

        protected bool Operate(ICommandContext context, Func<IOperationProgress, CancellationToken, Task> task, Func<Task> then = null) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            active.Operate(
                name: context.HasInput(out var input) ? input : GetType().Name,
                task: task,
                then: then);
            return true;
        }

        protected virtual bool Workable(ICommandContext context) {
            return true;
        }

        protected virtual Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            return Task.FromResult(false);
        }

        protected virtual async IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (context == null) yield break;
            if (context.HasLine(out var line)) {
                var command = line.HasCommand(out var c) ? c : "";
                var trigger = Trigger?.Input;
                if (trigger != null) {
                    var relevance = SuggestionRelevance.From(trigger.Options, command);
                    if (relevance.HasValue) {
                        yield return Suggestion(
                            context: context,
                            help: string.Join(" ", trigger.String, HelpLine),
                            input: trigger.String,
                            relevance: relevance.Value);
                    }
                }
            }
            await Task.CompletedTask;
        }

        protected string InputTrigger() {
            return Trigger?.Input?.String;
        }

        public virtual string Name => GetType().FullName;
        public virtual string HelpLine => null;
        public virtual bool Arbitrary => false;

        public ICommandTrigger Trigger { get; private set; }

        protected class ArgumentAttribute : CommandArgumentAttribute {
        }

        protected class SwitchAttribute : CommandSwitchAttribute {
        }

        bool ICommand.Workable(ICommandContext context) {
            return Workable(context);
        }

        Task<bool> ICommand.Work(ICommandContext context, CancellationToken cancellationToken) {
            return Work(context, cancellationToken);
        }

        IAsyncEnumerable<ICommandSuggestion> ICommand.Suggest(ICommandContext context, CancellationToken cancellationToken) {
            return Suggest(context, cancellationToken);
        }

        async Task ICommand.Init(CancellationToken cancellationToken) {
            Trigger = Trigger ?? await CommandTrigger.For(this, cancellationToken);
        }
    }

    public abstract class Command<TParameter> : Command where TParameter : new() {
        private Cli.ICommandLine CommandLine {
            get => _CommandLine ?? (_CommandLine = new CommandLine());
            set => _CommandLine = value;
        }
        private Cli.ICommandLine _CommandLine;

        private ICommandHelp Help =>
            _Help ?? (
            _Help = CommandLine.Helper.Help(typeof(TParameter)));
        private ICommandHelp _Help;

        private Context NewContext(ICommandContext context) {
            return new Context(context, CommandLine.Parser);
        }

        protected virtual bool Workable(Context context) =>
            base.Workable(context);

        protected virtual Task<bool> Work(Context context, CancellationToken cancellationToken) =>
            base.Work(context, cancellationToken);

        protected virtual IAsyncEnumerable<ICommandSuggestion> Suggest(Context context, CancellationToken cancellationToken) =>
            base.Suggest(context, cancellationToken);

        protected sealed override bool Workable(ICommandContext context) =>
            Workable(NewContext(context));

        protected sealed override Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) =>
            Work(NewContext(context), cancellationToken);

        protected sealed override IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, CancellationToken cancellationToken) =>
            Suggest(NewContext(context), cancellationToken);

        protected class Context : CommandContext<TParameter> {
            protected override TParameter Factory() {
                return new TParameter();
            }

            public Context(ICommandContext agent, ICommandParser parser) : base(agent, parser) {
            }
        }

        public override string HelpLine =>
            Help.HelpLine;
    }
}
