namespace Brows.Commands {
    internal sealed class ResetAll : Command {
        protected sealed override bool TriggeredWork(ICommandContext context) {
            if (context == null) return false;
            if (context.HasPanels(out var collection) == false) {
                return false;
            }
            if (collection is not PanelCollection panels) {
                return false;
            }
            foreach (var panel in panels) {
                panel.Reset();
            }
            return true;
        }
    }
}
