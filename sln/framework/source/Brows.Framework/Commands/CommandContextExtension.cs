using System.Threading.Tasks;

namespace Brows.Commands;

/// <summary>
/// Provides helpers for opening providers and showing the command palette.
/// </summary>
public static class CommandContextExtension {
    /// <summary>
    /// Opens a provider in the requested panel or window, using a wider scope when needed.
    /// </summary>
    /// <param name="commandContext">
    /// The context in which to open the provider.
    /// </param>
    /// <param name="id">
    /// The provider identifier to open.
    /// </param>
    /// <param name="how">
    /// Where to open the provider.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the provider was opened; otherwise, false.
    /// </returns>
    public static async Task<bool>
    Provide(this ICommandContext commandContext, string id, CommandContextProvide how, CancellationToken token) {
        if (commandContext == null) throw new ArgumentNullException(nameof(commandContext));
        switch (how) {
            case CommandContextProvide.ActivePanel:
                if (commandContext.HasPanel(out var active)) {
                    return await active.Provide(id, token);
                }
                goto case CommandContextProvide.AddPanel;
            case CommandContextProvide.AddPanel:
                if (commandContext.HasCommander(out var commander)) {
                    return await commander.AddPanel(id, token);
                }
                goto case CommandContextProvide.AddCommander;
            case CommandContextProvide.AddCommander:
                if (commandContext.HasDomain(out var domain)) {
                    return await domain.AddCommander(new[] { id }, token);
                }
                goto default;
            default:
                return false;
        }
    }

    /// <summary>
    /// Schedules the palette to open with initial input and no text selected.
    /// </summary>
    /// <param name="commandContext">
    /// The context in which to show the palette.
    /// </param>
    /// <param name="input">
    /// The initial input text.
    /// </param>
    /// <returns>
    /// True if the request was scheduled; otherwise, false.
    /// </returns>
    public static bool ShowPalette(this ICommandContext commandContext, string input) {
        return ShowPalette(commandContext, input, 0, 0);
    }

    /// <summary>
    /// Schedules the palette to open with initial input and an optional text selection.
    /// </summary>
    /// <param name="commandContext">
    /// The context in which to show the palette.
    /// </param>
    /// <param name="input">
    /// The initial input text.
    /// </param>
    /// <param name="selectedStart">
    /// The zero-based start of the text selection.
    /// </param>
    /// <param name="selectedLength">
    /// The number of characters to select.
    /// </param>
    /// <returns>
    /// True if the request was scheduled; otherwise, false.
    /// </returns>
    public static bool
    ShowPalette(this ICommandContext commandContext, string input, int selectedStart, int selectedLength) {
        ArgumentNullException.ThrowIfNull(commandContext);
        return commandContext.ShowPalette(new CommandPaletteConfig {
            Input = input,
            SelectedLength = selectedLength,
            SelectedStart = selectedStart
        });
    }
}
