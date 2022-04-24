using System;

namespace Brows.Cli {
    internal class CommandParseException : Exception {
        public CommandParseException(string invalid) : base() {
        }
    }
}
