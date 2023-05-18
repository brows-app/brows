namespace Brows.Commands {
    internal sealed class Palette : Command {
        protected sealed override bool TriggeredWork(ICommandContext context) {
            if (null == context) return false;
            return context.ShowPalette("");
        }
    }
}
