namespace Brows.Commands {
    internal sealed class Palette : Command {
        protected sealed override bool TriggeredWork(ICommandContext context) {
            if (context == null) return false;
            if (context.HasCommander(out var commander) == false) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                return await commander.ShowPalette("", token);
            });
        }
    }
}
