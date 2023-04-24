namespace Brows.Commands {
    internal sealed class Escape : Command {
        protected sealed override bool TriggeredWork(ICommandContext context) {
            return false;
        }
    }
}
