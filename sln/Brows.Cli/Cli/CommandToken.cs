using System;

namespace Brows.Cli {
    internal class CommandToken {
        public bool IsSwitch =>
            IsLongSwitch ||
            IsShortSwitch;

        public bool IsLongSwitch =>
            Input.Length > 2 &&
            Input[0] == '-' &&
            Input[1] == '-' &&
            Input[2] != '-';

        public bool IsShortSwitch =>
            Input.Length > 1 &&
            Input[0] == '-' &&
            Input[1] != '-';

        public string Switch =>
            IsLongSwitch ? Input.Substring(2) :
            IsShortSwitch ? Input.Substring(1) :
            null;

        public char? Quote =>
            (Input.StartsWith('\'') && Input.EndsWith('\'')) ? '\'' :
            (Input.StartsWith('"') && Input.EndsWith('"')) ? '"' :
            default(char?);

        public string Argument =>
            Quote.HasValue
                ? Input.Trim(Quote.Value)
                : Input;

        public string Input { get; }

        public CommandToken(string input) {
            Input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public object ConvertTo(Type type) {
            if (type is null) throw new ArgumentNullException(nameof(type));
            if (type.IsEnum) {
                var parsed = Enum.TryParse(type, Argument, ignoreCase: true, out var result);
                if (parsed) {
                    return result;
                }
            }
            return Convert.ChangeType(Argument, type);
        }
    }
}
