namespace Brows.Commands {
    internal sealed class HistoryForward : Command {
        protected sealed override bool TriggeredWork(ICommandContext context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) {
                return false;
            }
            if (active is not Panel panel) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                return await panel.HistoryForward(token);
            });
        }
    }
}
