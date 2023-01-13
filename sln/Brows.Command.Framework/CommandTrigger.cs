using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Config;

    internal class CommandTrigger : ICommandTrigger {
        private readonly ITriggerInput Input;
        private readonly ITriggerPressCollection Press;

        private CommandTrigger(ITriggerInput input, ITriggerPressCollection press) {
            Input = input;
            Press = press;
        }

        public static async Task<CommandTrigger> For(Command command, CancellationToken cancellationToken) {
            if (null == command) throw new ArgumentNullException(nameof(command));
            var type = command.GetType();
            var name = type.Name;
            var conf = await Configure.File<Key>().Load(cancellationToken);
            if (conf.Cmd.TryGetValue(name, out var cmd) == false) {
                return null;
            }
            return new CommandTrigger(cmd.Input(), cmd.Press());
        }

        ITriggerInput ICommandTrigger.Input => Input;
        ITriggerPressCollection ICommandTrigger.Press => Press;
    }
}
