using System;

namespace Brows {
    internal class CommandLine : ICommandLine {
        private static readonly char[] Split = new[] { ' ' };

        private string TrimStart =>
            _TrimStart ?? (
            _TrimStart = Input.TrimStart(' '));
        private string _TrimStart;

        private string[] Parts =>
            _Parts ?? (
            _Parts = TrimStart.Split(Split, 2, StringSplitOptions.RemoveEmptyEntries));
        private string[] _Parts;

        private string Parameter =>
            _Parameter ?? (
            _Parameter = Parts.Length > 1 ? Parts[1].TrimEnd(' ') : "");
        private string _Parameter;

        private bool Triggered =>
            _Triggered ?? (
            _Triggered =
                TrimStart.Length > Command.Length &&
                TrimStart[Command.Length] == ' ').Value;
        private bool? _Triggered;

        public string Command =>
            _Command ?? (
            _Command = Parts.Length > 0 ? Parts[0] : "");
        private string _Command;

        public string Input { get; }

        public CommandLine(string input) {
            Input = input ?? throw new ArgumentNullException(nameof(input));
        }

        bool ICommandLine.HasTrigger(out string trigger) {
            trigger = Command;
            return Triggered;
        }

        bool ICommandLine.HasInput(out string input) {
            input = Input;
            return Parts.Length > 0;
        }

        bool ICommandLine.HasCommand(out string command) {
            command = Command;
            return command.Length > 0;
        }

        bool ICommandLine.HasParameter(out string parameter) {
            parameter = Parameter;
            return parameter.Length > 0;
        }
    }
}
