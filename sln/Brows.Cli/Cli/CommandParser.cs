using System;
using System.Linq;

namespace Brows.Cli {
    internal class CommandParser : ICommandParser {
        private CommandTokenizer Tokenizer {
            get => _Tokenizer ?? (_Tokenizer = new CommandTokenizer());
            set => _Tokenizer = value;
        }
        private CommandTokenizer _Tokenizer;

        private CommandReflector Reflector {
            get => _Reflector ?? (_Reflector = new CommandReflector());
            set => _Reflector = value;
        }
        private CommandReflector _Reflector;

        public void Parse(string s, object obj) {
            if (null == obj) throw new ArgumentNullException(nameof(obj));
            var type = obj.GetType();
            var reflect = Reflector.Reflect(type);
            var argProperties = reflect.ArgumentProperties();
            var argOrd = 0;
            var tokens = Tokenizer.Tokenize(s).ToArray();
            for (var i = 0; i < tokens.Length; i++) {
                var token = tokens[i];
                if (token.IsLongSwitch) {
                    var switchProperty = reflect.SwitchProperty(token.Switch);
                    if (switchProperty == null) throw new CommandParseException(token.Input);

                    var switchArgs = tokens.Skip(i + 1).TakeWhile(token => !token.IsSwitch).ToArray();
                    var setArgCount = switchProperty.Set(switchArgs, obj);
                    if (setArgCount != switchArgs.Length) throw new CommandParseException(token.Input);
                    i += setArgCount;
                    continue;
                }
                if (token.IsShortSwitch) {
                    var switchNames = token.Switch.ToArray();
                    var switchProperties = reflect.SwitchProperties(switchNames);
                    if (switchProperties.Length != switchNames.Length) throw new CommandParseException(token.Input);
                    for (var j = 0; j < switchProperties.Length; j++) {
                        var switchProperty = switchProperties[j];
                        if (j == switchProperties.Length - 1) {
                            var switchArgs = tokens.Skip(i + 1).TakeWhile(token => !token.IsSwitch).ToArray();
                            var setArgCount = switchProperty.Set(switchArgs, obj);
                            if (setArgCount != switchArgs.Length) throw new CommandParseException(token.Input);
                            i += setArgCount;
                        }
                        else {
                            switchProperty.Set(new CommandToken[0], obj);
                        }
                    }
                    continue;
                }
                var argSet = false;
                for (var j = 0; j < argProperties.Length; j++) {
                    var argSetCount = 0;
                    var argProperty = argProperties[j];
                    if (argProperty.Aggregate) {
                        var arguments = tokens.Skip(i + j).TakeWhile(token => !token.IsSwitch).ToArray();
                        var count = argSetCount = argProperty.Set(arguments, argOrd, obj);
                        if (count == 0) throw new CommandParseException(token.Input);
                        argSet = true;
                        argOrd++;
                        j += (argSetCount - 1);
                    }
                    else {
                        var count = argSetCount = argProperty.Set(new[] { tokens[i + j] }, argOrd, obj);
                        if (count > 0) {
                            argSet = true;
                            argOrd++;
                        }
                        else {
                            argSet = false;
                        }
                    }
                    i += j;
                }
                if (argSet == false) throw new CommandParseException(token.Input);
            }
        }
    }
}
