namespace Brows.Commands {
    internal sealed class Cycle : Command {
        protected sealed override bool TriggeredWork(ICommandContext context) {
            if (context == null) return false;
            if (context.HasPanels(out var collection) == false) {
                return false;
            }
            if (collection is not PanelCollection panels) {
                return false;
            }
            for (var i = 0; i < panels.Count; i++) {
                var panel = panels[i];
                if (panel.Active) {
                    if (i < panels.Count - 1) {
                        panels[i + 1].Activate();
                    }
                    else {
                        panels[0].Activate();
                    }
                    break;
                }
            }
            return true;
        }
    }
}
