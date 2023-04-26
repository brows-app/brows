namespace Brows.Commands {
    internal sealed class AddPanel : Command {
        protected sealed override bool TriggeredWork(ICommandContext context) {
            if (context == null) return false;
            if (context.HasCommander(out var commander) == false) {
                return false;
            }
            var id = context.HasPanel(out var active) && active.HasProvider(out IProvider provider)
                ? provider.ID
                : "";
            return context.Operate(async (progress, token) => {
                return await commander.AddPanel(id, token);
            });
        }
    }
}
