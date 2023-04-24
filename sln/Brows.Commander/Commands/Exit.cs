namespace Brows.Commands {
    internal sealed class Exit : Command {
        protected sealed override bool TriggeredWork(ICommandContext context) {
            if (context == null) return false;
            if (context.HasCommander(out var commander) == false) {
                return false;
            }
            if (commander is not Commander c) {
                return false;
            }
            var service = c.Domain;
            if (service == null) {
                return false;
            }
            service.End();
            return true;
        }
    }
}
