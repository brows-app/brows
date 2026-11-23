using Brows.Composition;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Commands;

/// <summary>
/// Defines a user action, its triggers, and its command palette suggestions.
/// </summary>
public interface ICommand : IExport {
    /// <summary>
    /// Occurs when the configured triggers change.
    /// </summary>
    event EventHandler TriggerChanged;

    /// <summary>
    /// Gets whether the command is enabled.
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// Gets the command configuration.
    /// </summary>
    object Config { get; }

    /// <summary>
    /// Gets the configured input and gesture triggers.
    /// </summary>
    ICommandTrigger Trigger { get; }

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
    /// The suggestions available for the input and source.
    /// </returns>
    IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, CancellationToken token);

    /// <summary>
    /// Performs the command when its configured input or gesture has been triggered.
    /// </summary>
    /// <param name="context">
    /// The context in which the command is invoked.
    /// </param>
    /// <returns>
    /// True if the command handled the request; otherwise, false.
    /// </returns>
    bool TriggeredWork(ICommandContext context);

    /// <summary>
    /// Offers input to the command without requiring a configured trigger match.
    /// </summary>
    /// <param name="context">
    /// The context containing the offered input.
    /// </param>
    /// <returns>
    /// True if the command handled the request; otherwise, false.
    /// </returns>
    bool ArbitraryWork(ICommandContext context);

    /// <summary>
    /// Initializes the command before it is used.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when initialization finishes.
    /// </returns>
    Task Init(CancellationToken token);
}
