using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public static class CommandContextExtension {
        public static async Task<bool> Provide(this ICommandContext commandContext, string id, CommandContextProvide how, CancellationToken token) {
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

        public static bool ShowPalette(this ICommandContext commandContext, string input) {
            return ShowPalette(commandContext, input, 0, 0);
        }

        public static bool ShowPalette(this ICommandContext commandContext, string input, int selectedStart, int selectedLength) {
            ArgumentNullException.ThrowIfNull(commandContext);
            return commandContext.ShowPalette(new CommandPaletteConfig {
                Input = input,
                SelectedLength = selectedLength,
                SelectedStart = selectedStart
            });
        }
    }
}
