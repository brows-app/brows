using System;

namespace Brows.Cli {
    internal class CommandHelper : ICommandHelper {
        public CommandReflector Reflector {
            get => _Reflector ?? (_Reflector = new CommandReflector());
            set => _Reflector = value;
        }
        private CommandReflector _Reflector;

        public ICommandHelp Help(Type type) {
            return new CommandHelp(Reflector.Reflect(type));
        }
    }
}
