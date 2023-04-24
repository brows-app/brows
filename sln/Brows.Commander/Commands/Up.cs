namespace Brows.Commands {
    internal sealed class Up : Command {
        protected sealed override bool TriggeredWork(ICommandContext context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) {
                return false;
            }
            if (active.HasProvider(out IEntryProvider provider) == false) {
                return false;
            }
            var parent = provider.Parent?.Trim() ?? "";
            if (parent == "") {
                return false;
            }
            return context.Operate(async (progress, token) => {
                return await active.Provide(parent, token);
            });
        }
    }
}
