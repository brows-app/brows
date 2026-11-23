using Brows.Localization;
using Brows.Operations;
using Brows.Panels;
using Brows.Providers;
using Brows.Triggers;
using Domore.Conf.Cli;
using Domore.Conf.Extensions;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Brows.Commands;

/// <summary>
/// Provides configurable triggers, suggestions, and context checks for a user command.
/// </summary>
public abstract class Command : ICommand {
    private static readonly ILog Log = Logging.For(typeof(Command));

    private readonly CommandHistory History = new();

    private bool Valid(ICommandContext context) {
        if (Enabled == false) {
            return false;
        }
        if (Provided(context) == false) {
            return false;
        }
        if (Sourced(context) == false) {
            return false;
        }
        if (Observed(context) == false) {
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

    private bool Observed(ICommandContext context) {
        if (RequiresEntries == false) {
            return true;
        }
        if (context == null) return false;
        if (context.HasPanel(out var active) == false) {
            return false;
        }
        return active.HasProvider(out IEntryProvider _);
    }

    private protected CliProvider CliProvider => field ??=
        (CommandCliProvider ?? throw new InvalidOperationException("No command CLI provider!")).CliProvider;

    [ImportRequired]
    internal CommandCliProvider CommandCliProvider { get; set; }

    internal ITranslationExport Translate { get; set; }

    internal string Name {
        get {
            if (field == null) {
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
                field = name;
            }
            return field;
        }
    }

    /// <summary>
    /// Gets whether the command is enabled by its configuration.
    /// </summary>
    protected virtual bool Enabled =>
        Config?.Enabled != false;

    /// <summary>
    /// Gets the configuration that controls the command.
    /// </summary>
    protected virtual CommandConfig Config => field ??=
        new CommandConfig();

    /// <summary>
    /// Gets the helper used to rank matching input triggers.
    /// </summary>
    protected CommandSuggestionRelevance SuggestionRelevance => field ??=
        new CommandSuggestionRelevance();

    /// <summary>
    /// Gets the primary configured input trigger, or null when none is configured.
    /// </summary>
    protected string InputTrigger =>
        Trigger?.Inputs?.Main?.Input;

    /// <summary>
    /// Gets the exact provider type required by the command, or null to allow any provider type.
    /// </summary>
    protected virtual Type Provider =>
        null;

    /// <summary>
    /// Gets the allowed source item types, or null to allow any source type.
    /// </summary>
    /// <remarks>
    /// The current source item must match an allowed type, and one allowed type must accept every item
    /// in the source collection.
    /// </remarks>
    protected virtual IEnumerable<Type> Source =>
        null;

    /// <summary>
    /// Gets whether the command works only in a panel whose provider provides entries.
    /// </summary>
    /// <value>
    /// True if the command is neither triggered nor suggested when the provider of the active panel
    /// provides no entries. The default is false.
    /// </value>
    protected virtual bool RequiresEntries =>
        false;

    /// <summary>
    /// Creates a command suggestion using the supplied text and optional display settings.
    /// </summary>
    /// <param name="context">
    /// The context for the suggestion.
    /// </param>
    /// <param name="input">
    /// The input inserted when the suggestion is chosen.
    /// </param>
    /// <param name="help">
    /// The help text, or null to use the input.
    /// </param>
    /// <param name="group">
    /// The translation key for the group name.
    /// </param>
    /// <param name="relevance">
    /// The ranking score, or null for zero.
    /// </param>
    /// <param name="description">
    /// The description, or null for the command description.
    /// </param>
    /// <param name="press">
    /// The shortcut display text, or null for the configured gestures.
    /// </param>
    /// <param name="alias">
    /// The alias display text, or null for the configured input aliases.
    /// </param>
    /// <param name="history">
    /// Whether the suggestion is from history; null means false.
    /// </param>
    /// <param name="groupOrder">
    /// The group order, or null for zero.
    /// </param>
    /// <returns>
    /// The command suggestion.
    /// </returns>
    protected ICommandSuggestion Suggestion(ICommandContext context,
                                            string input,
                                            string help = null,
                                            string group = null,
                                            int? relevance = null,
                                            string description = null,
                                            string press = null,
                                            string alias = null,
                                            bool? history = null,
                                            int? groupOrder = null) {
        return new CommandSuggestion {
            Description = description ?? CommandTranslation.Description(Translate, Name),
            Group = CommandTranslation.Group(Translate, group),
            GroupOrder = groupOrder ?? 0,
            Help = help ?? input,
            History = history ?? false,
            Input = input,
            Alias = alias ?? string.Join(",", Trigger?.Inputs?.Main?.Aliases?.AsEnumerable() ?? Array.Empty<string>()),
            Press = press ?? string.Join(",", Trigger?.Gestures?.Select(p => p.Display) ?? Array.Empty<string>()),
            Relevance = relevance ?? 0
        };
    }

    /// <summary>
    /// Performs the command when its configured input or gesture has been triggered.
    /// </summary>
    /// <param name="context">
    /// The context in which the command is invoked.
    /// </param>
    /// <returns>
    /// True if the command handled the request; otherwise, false.
    /// </returns>
    protected virtual bool TriggeredWork(ICommandContext context) {
        return false;
    }

    /// <summary>
    /// Offers input to the command without requiring a configured trigger match.
    /// </summary>
    /// <param name="context">
    /// The context containing the offered input.
    /// </param>
    /// <returns>
    /// True if the command handled the request; otherwise, false.
    /// </returns>
    protected virtual bool ArbitraryWork(ICommandContext context) {
        return false;
    }

    /// <summary>
    /// Initializes the command before it is used.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when initialization finishes.
    /// </returns>
    protected virtual Task Init(CancellationToken token) {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Produces command palette suggestions for the current context.
    /// </summary>
    /// <param name="context">
    /// The context for which suggestions are requested.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The suggestions for the matching input triggers and their command history.
    /// </returns>
    protected virtual async
    IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context,
                                                 [EnumeratorCancellation] CancellationToken token) {
        if (context == null) yield break;
        if (context.DidTrigger(out var triggered)) {
            if (triggered.Contains(this) == false) {
                yield break;
            }
        }
        if (context.HasLine(out var line)) {
            var command = line.HasCommand(out var c) ? c : "";
            var triggers = Trigger?.Inputs;
            if (triggers != null) {
                foreach (var trigger in triggers) {
                    var relevance = SuggestionRelevance.From(trigger.Options, command);
                    if (relevance.HasValue) {
                        if (trigger.Defined != null) {
                            yield return Suggestion(
                                alias: string.Join(",", trigger.Aliases?.AsEnumerable() ?? Array.Empty<string>()),
                                press: string.Join(",", Trigger.Gestures
                                    ?.Where(g => g.Defined == trigger.Defined)
                                    ?.Select(g => g.Display) ?? Array.Empty<string>()),
                                context: context,
                                description: trigger.Defined,
                                help: trigger.Input + " " + HelpLine,
                                input: trigger.Input,
                                relevance: relevance.Value);
                        }
                        else {
                            yield return Suggestion(
                                context: context,
                                help: trigger.Input + " " + HelpLine,
                                input: trigger.Input,
                                relevance: relevance.Value);
                        }
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
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Occurs when the configured triggers change.
    /// </summary>
    public event EventHandler TriggerChanged;

    /// <summary>
    /// Gets the parameter help displayed after the command name.
    /// </summary>
    public virtual string HelpLine => CommandTranslation.Help(Translate, Name);

    /// <summary>
    /// Gets the configuration help displayed for the command.
    /// </summary>
    public virtual string ConfText => CommandTranslation.Conf(this);

    /// <summary>
    /// Gets the configured input and gesture triggers after command initialization.
    /// </summary>
    public ICommandTrigger Trigger { get; private set; }

    bool ICommand.Enabled => Enabled;
    object ICommand.Config => Config;

    bool ICommand.TriggeredWork(ICommandContext context) {
        if (Valid(context) == false) {
            return false;
        }
        var worked = TriggeredWork(context);
        if (worked && context is not null) {
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
        if (Log.Info()) {
            Log.Info(Log.Join(nameof(Init), this));
        }
        Trigger = await CommandTrigger.For(this, token);
        CommandTrigger.Changed += (s, e) => {
            Trigger = CommandTrigger.For(this);
            TriggerChanged?.Invoke(this, EventArgs.Empty);
        };
        await Init(token);
    }
}

/// <summary>
/// Provides a command with parsed parameters and a context that reports parsing errors.
/// </summary>
/// <typeparam name="TParameter">
/// The command parameter type, which must have a public parameterless constructor.
/// </typeparam>
public abstract class Command<TParameter> : Command where TParameter : new() {
    private Context Proxy(ICommandContext context) {
        return new Context(context, this);
    }

    private CliProvider Cli => CliProvider;

    /// <summary>
    /// Gets the parameter help generated from the parameter type.
    /// </summary>
    public sealed override string HelpLine => field ??=
        Cli.Display(new TParameter()).Split([' '], 2).LastOrDefault();

    /// <summary>
    /// Creates command parameters from configuration text and command-line arguments.
    /// </summary>
    /// <param name="parameter">
    /// The command-line arguments.
    /// </param>
    /// <param name="conf">
    /// The command configuration text.
    /// </param>
    /// <returns>
    /// The populated parameter object. Command-line values take precedence over configuration text.
    /// </returns>
    protected virtual TParameter ParameterFactory(string parameter, string conf) {
        var
        inst = new TParameter();
        inst = inst.ConfFrom(conf, key: "");
        inst = Cli.Configure(inst, parameter);
        return inst;
    }

    /// <summary>
    /// Performs the command with access to parsed parameters.
    /// </summary>
    /// <param name="context">
    /// The command context with parameter access.
    /// </param>
    /// <returns>
    /// True if the command handled the request; otherwise, false.
    /// </returns>
    protected virtual bool Work(Context context) =>
        base.TriggeredWork(context);

    /// <summary>
    /// Produces palette suggestions with access to the command parameters and configuration help.
    /// </summary>
    /// <param name="context">
    /// The context for which suggestions are requested.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The suggestions available for the context.
    /// </returns>
    protected virtual IAsyncEnumerable<ICommandSuggestion> Suggest(Context context, CancellationToken token) {
        if (context is not null) {
            if (context.DidTrigger(this)) {
                var confText = ConfText;
                if (confText is not null) {
                    if (context.HasConf(out var conf) == false || conf.Command != this) {
                        context.SetConf(new CommandContextConf(this, confText));
                    }
                    return CommandSuggestions.Empty();
                }
            }
        }
        return base.Suggest(context, token);
    }

    /// <summary>
    /// Offers untriggered input to the base implementation, which handles none, and prevents further overriding.
    /// </summary>
    /// <param name="context">
    /// The context containing the offered input.
    /// </param>
    /// <returns>
    /// True if the command handled the request; otherwise, false.
    /// </returns>
    protected sealed override bool ArbitraryWork(ICommandContext context) =>
        base.ArbitraryWork(context);

    /// <summary>
    /// Runs the command through its parameter-aware context.
    /// </summary>
    /// <param name="context">
    /// The context in which the command is invoked.
    /// </param>
    /// <returns>
    /// True if the command handled the request; otherwise, false.
    /// </returns>
    protected sealed override bool TriggeredWork(ICommandContext context) =>
        context is not null && Work(Proxy(context));

    /// <summary>
    /// Produces palette suggestions with access to the command parameters and configuration help.
    /// </summary>
    /// <param name="context">
    /// The context for which suggestions are requested.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The suggestions available for the context.
    /// </returns>
    protected sealed override IAsyncEnumerable<ICommandSuggestion>
    Suggest(ICommandContext context, CancellationToken token) =>
        context is null
            ? CommandSuggestions.Empty()
            : Suggest(Proxy(context), token);

    /// <summary>
    /// Provides command context operations and access to parsed command parameters.
    /// </summary>
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

        /// <summary>
        /// Tries to parse parameters when arguments or configuration text were supplied.
        /// </summary>
        /// <param name="parameter">
        /// Receives the parsed parameters on success, or the default value on failure.
        /// </param>
        /// <returns>
        /// True if parameters were supplied and parsed successfully; otherwise, false.
        /// </returns>
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

        /// <summary>
        /// Tries to get the error encountered while parsing parameters.
        /// </summary>
        /// <param name="error">
        /// Receives the parsing error, or null when no error occurred.
        /// </param>
        /// <returns>
        /// True if parameter parsing failed; otherwise, false.
        /// </returns>
        public bool HasParameterError(out Exception error) {
            if (HasParameter(out _)) {
                error = null;
                return false;
            }
            error = ParameterException;
            return error is not null;
        }

        /// <summary>
        /// Gets parsed parameters, or creates default parameters when none were supplied.
        /// </summary>
        /// <param name="parameter">
        /// Receives the parsed or default parameters on success.
        /// </param>
        /// <returns>
        /// True if parameters are available; false if parsing failed.
        /// </returns>
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

        /// <inheritdoc/>
        public void SetConf(ICommandContextConf conf) {
            Agent.SetConf(conf);
        }

        /// <inheritdoc/>
        public bool HasConf(out ICommandContextConf conf) {
            return Agent.HasConf(out conf);
        }

        /// <inheritdoc/>
        public void SetData(ICommandContextData data) {
            Agent.SetData(data);
        }

        /// <inheritdoc/>
        public bool HasData(out ICommandContextData data) {
            return Agent.HasData(out data);
        }

        /// <inheritdoc/>
        public void SetFlag(ICommandContextFlag flag) {
            Agent.SetFlag(flag);
        }

        /// <inheritdoc/>
        public bool HasFlag(out ICommandContextFlag flag) {
            return Agent.HasFlag(out flag);
        }

        /// <inheritdoc/>
        public bool HasPalette(out ICommandPalette palette) {
            return Agent.HasPalette(out palette);
        }

        /// <inheritdoc/>
        public bool HasCommander(out ICommander commander) {
            return Agent.HasCommander(out commander);
        }

        /// <inheritdoc/>
        public bool HasDomain(out ICommanderDomain domain) {
            return Agent.HasDomain(out domain);
        }

        /// <inheritdoc/>
        public bool HasInput(out string value) {
            return Agent.HasInput(out value);
        }

        /// <inheritdoc/>
        public bool HasPanel(out IPanel active) {
            return Agent.HasPanel(out active);
        }

        /// <inheritdoc/>
        public bool HasPanel(int column, out IPanel panel) {
            return Agent.HasPanel(column, out panel);
        }

        /// <inheritdoc/>
        public bool HasPanels(out IPanelCollection collection) {
            return Agent.HasPanels(out collection);
        }

        /// <inheritdoc/>
        public bool HasPanels(PanelPassiveMode mode, out IPanel active, out IPanel passive) {
            return Agent.HasPanels(mode, out active, out passive);
        }

        /// <inheritdoc/>
        public bool HasGesture(out IGesture gesture) {
            return Agent.HasGesture(out gesture);
        }

        /// <inheritdoc/>
        public bool HasLine(out ICommandLine line) {
            return Agent.HasLine(out line);
        }

        /// <inheritdoc/>
        public bool MayTrigger(ICommand command) {
            return Agent.MayTrigger(command);
        }

        /// <inheritdoc/>
        public bool DidTrigger(ICommand command) {
            return Agent.DidTrigger(command);
        }

        /// <inheritdoc/>
        public bool DidTrigger(out IReadOnlySet<ICommand> commands) {
            return Agent.DidTrigger(out commands);
        }

        /// <inheritdoc/>
        public bool Operate(Func<IOperationProgress, CancellationToken, Task<bool>> task) {
            return Agent.Operate(task);
        }

        /// <inheritdoc/>
        public void SetHint(ICommandContextHint hint) {
            Agent.SetHint(hint);
        }

        /// <inheritdoc/>
        public bool HasSource<T>(out T item, out IReadOnlyList<T> items) {
            return Agent.HasSource(out item, out items);
        }

        /// <inheritdoc/>
        public bool HasSource(out ICommandSource source) {
            return Agent.HasSource(out source);
        }

        /// <inheritdoc/>
        public bool ShowPalette(ICommandPaletteConfig config) {
            return Agent.ShowPalette(config);
        }
    }
}
