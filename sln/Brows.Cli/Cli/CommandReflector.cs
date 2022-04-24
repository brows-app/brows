using System;
using System.Collections.Generic;

namespace Brows.Cli {
    internal class CommandReflector {
        private static readonly Dictionary<Type, CommandReflection> Cache = new Dictionary<Type, CommandReflection>();

        public CommandReflection Reflect(Type type) {
            if (Cache.TryGetValue(type, out var value) == false) {
                Cache[type] = value = new CommandReflection(type);
            }
            return value;
        }
    }
}
