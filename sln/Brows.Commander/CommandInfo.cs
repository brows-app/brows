using System;

namespace Brows {
    internal class CommandInfo : ICommandInfo {
        private string[] Parts =>
            _Parts ?? (
            _Parts = Input.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries));
        private string[] _Parts;

        public string Command =>
            _Command ?? (
            _Command = Parts.Length > 0 ? Parts[0] : "");
        private string _Command;

        public string Parameter =>
            _Parameter ?? (
            _Parameter = Parts.Length > 1 ? Parts[1] : "");
        private string _Parameter;

        public string Input { get; }

        public CommandInfo(string input) {
            Input = input ?? throw new ArgumentNullException(nameof(input));
        }
    }
}
