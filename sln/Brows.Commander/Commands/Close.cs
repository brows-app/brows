namespace Brows.Commands {
    internal sealed class Close : Command {
        protected sealed override bool TriggeredWork(ICommandContext context) {
            if (context == null) return false;
            if (context.HasCommander(out var commander) == false) {
                return false;
            }
            if (commander is not Commander c) {
                return false;
            }
            c.Close();
            return true;
        }
    }
}
