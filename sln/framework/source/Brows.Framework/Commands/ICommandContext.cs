using Brows.Operations;
using Brows.Panels;
using Brows.Triggers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Commands;

/// <summary>
/// Provides the panels, input, services, and user feedback available to a command.
/// </summary>
public interface ICommandContext {
    /// <summary>
    /// Sets configuration help for this context.
    /// </summary>
    /// <param name="conf">
    /// The configuration help to apply.
    /// </param>
    void SetConf(ICommandContextConf conf);

    /// <summary>
    /// Tries to get the configuration text.
    /// </summary>
    /// <param name="conf">
    /// Receives the configuration text when available.
    /// </param>
    /// <returns>
    /// True if the requested value is available; otherwise, false.
    /// </returns>
    bool HasConf(out ICommandContextConf conf);

    /// <summary>
    /// Sets a command hint for this context.
    /// </summary>
    /// <param name="hint">
    /// The command hint to apply.
    /// </param>
    void SetHint(ICommandContextHint hint);

    /// <summary>
    /// Sets browsable command data for this context.
    /// </summary>
    /// <param name="data">
    /// The browsable command data to apply.
    /// </param>
    void SetData(ICommandContextData data);

    /// <summary>
    /// Tries to get the browsable command data.
    /// </summary>
    /// <param name="data">
    /// Receives the browsable command data when available.
    /// </param>
    /// <returns>
    /// True if the requested value is available; otherwise, false.
    /// </returns>
    bool HasData(out ICommandContextData data);

    /// <summary>
    /// Sets palette input behavior for this context.
    /// </summary>
    /// <param name="flag">
    /// The palette input behavior to apply.
    /// </param>
    void SetFlag(ICommandContextFlag flag);

    /// <summary>
    /// Tries to get the palette input behavior.
    /// </summary>
    /// <param name="flag">
    /// Receives the palette input behavior when available.
    /// </param>
    /// <returns>
    /// True if the requested value is available; otherwise, false.
    /// </returns>
    bool HasFlag(out ICommandContextFlag flag);

    /// <summary>
    /// Tries to get the palette that initiated this context.
    /// </summary>
    /// <param name="palette">
    /// Receives the palette that initiated this context when available.
    /// </param>
    /// <returns>
    /// True if the requested value is available; otherwise, false.
    /// </returns>
    bool HasPalette(out ICommandPalette palette);

    /// <summary>
    /// Tries to get the commander containing this context.
    /// </summary>
    /// <param name="commander">
    /// Receives the commander containing this context when available.
    /// </param>
    /// <returns>
    /// True if the requested value is available; otherwise, false.
    /// </returns>
    bool HasCommander(out ICommander commander);

    /// <summary>
    /// Tries to get the commander window manager.
    /// </summary>
    /// <param name="domain">
    /// Receives the commander window manager when available.
    /// </param>
    /// <returns>
    /// True if the requested value is available; otherwise, false.
    /// </returns>
    bool HasDomain(out ICommanderDomain domain);

    /// <summary>
    /// Tries to get the nonblank command input.
    /// </summary>
    /// <param name="value">
    /// Receives the nonblank command input when available.
    /// </param>
    /// <returns>
    /// True if the requested value is available; otherwise, false.
    /// </returns>
    bool HasInput(out string value);

    /// <summary>
    /// Tries to get the active panel.
    /// </summary>
    /// <param name="active">
    /// Receives the active panel when available.
    /// </param>
    /// <returns>
    /// True if the requested value is available; otherwise, false.
    /// </returns>
    bool HasPanel(out IPanel active);

    /// <summary>
    /// Tries to get a panel at a column position.
    /// </summary>
    /// <param name="column">
    /// The zero-based column position.
    /// </param>
    /// <param name="panel">
    /// Receives the panel when available.
    /// </param>
    /// <returns>
    /// True if the column contains a panel; otherwise, false.
    /// </returns>
    bool HasPanel(int column, out IPanel panel);

    /// <summary>
    /// Tries to get the nonempty panel collection.
    /// </summary>
    /// <param name="collection">
    /// Receives the nonempty panel collection when available.
    /// </param>
    /// <returns>
    /// True if the requested value is available; otherwise, false.
    /// </returns>
    bool HasPanels(out IPanelCollection collection);

    /// <summary>
    /// Tries to get the active panel and a destination chosen by the requested mode.
    /// </summary>
    /// <param name="mode">
    /// How to choose the destination panel.
    /// </param>
    /// <param name="active">
    /// Receives the active panel.
    /// </param>
    /// <param name="passive">
    /// Receives the chosen destination, which may be the active panel.
    /// </param>
    /// <returns>
    /// True if both panels can be chosen; otherwise, false.
    /// </returns>
    bool HasPanels(PanelPassiveMode mode, out IPanel active, out IPanel passive);

    /// <summary>
    /// Tries to get the gesture that initiated this context.
    /// </summary>
    /// <param name="gesture">
    /// Receives the gesture that initiated this context when available.
    /// </param>
    /// <returns>
    /// True if the requested value is available; otherwise, false.
    /// </returns>
    bool HasGesture(out IGesture gesture);

    /// <summary>
    /// Tries to get the parsed command line.
    /// </summary>
    /// <param name="line">
    /// Receives the parsed command line when available.
    /// </param>
    /// <returns>
    /// True if the requested value is available; otherwise, false.
    /// </returns>
    bool HasLine(out ICommandLine line);

    /// <summary>
    /// Determines whether the input or gesture matches one of a command's triggers.
    /// </summary>
    /// <param name="command">
    /// The command to check.
    /// </param>
    /// <returns>
    /// True if a configured trigger matches; otherwise, false.
    /// </returns>
    bool MayTrigger(ICommand command);

    /// <summary>
    /// Determines whether the input or gesture confirms a command trigger.
    /// </summary>
    /// <param name="command">
    /// The command to check.
    /// </param>
    /// <returns>
    /// True if the command is triggered. Text input must include a space after the command name.
    /// </returns>
    bool DidTrigger(ICommand command);

    /// <summary>
    /// Tries to get the commands triggered by the input or gesture.
    /// </summary>
    /// <param name="commands">
    /// Receives the matching commands.
    /// </param>
    /// <returns>
    /// True if commands are triggered. Text input must include a space after the command name.
    /// </returns>
    bool DidTrigger(out IReadOnlySet<ICommand> commands);

    /// <summary>
    /// Tries to get the command source.
    /// </summary>
    /// <param name="source">
    /// Receives the command source when available.
    /// </param>
    /// <returns>
    /// True if the requested value is available; otherwise, false.
    /// </returns>
    bool HasSource(out ICommandSource source);

    /// <summary>
    /// Tries to get the current source item and source collection as the requested type.
    /// </summary>
    /// <typeparam name="T">
    /// The required source item type.
    /// </typeparam>
    /// <param name="item">
    /// Receives the current source item.
    /// </param>
    /// <param name="items">
    /// Receives source collection items of the requested type.
    /// </param>
    /// <returns>
    /// True if the current item has the requested type and the filtered collection is nonempty; otherwise, false.
    /// </returns>
    bool HasSource<T>(out T item, out IReadOnlyList<T> items);

    /// <summary>
    /// Schedules the command palette to open with the supplied settings.
    /// </summary>
    /// <param name="config">
    /// The initial input, selection, and suggestion settings.
    /// </param>
    /// <returns>
    /// True if the request was scheduled; otherwise, false.
    /// </returns>
    bool ShowPalette(ICommandPaletteConfig config);

    /// <summary>
    /// Schedules cancellable background work with operation progress.
    /// </summary>
    /// <param name="task">
    /// The work to run, receiving progress reporting and a cancellation token.
    /// </param>
    /// <returns>
    /// True if the work was scheduled; otherwise, false. This does not report the work's eventual result.
    /// </returns>
    bool Operate(Func<IOperationProgress, CancellationToken, Task<bool>> task);
}
