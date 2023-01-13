using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Config {
    internal class Cmd {
        public string Inp { get; set; }

        public Dictionary<string, string> Key {
            get => _Key ?? (_Key = new());
            set => _Key = value;
        }
        private Dictionary<string, string> _Key;

        public ITriggerInput Input() {
            var inp = Inp?.Trim() ?? "";
            if (inp == "") {
                return null;
            }
            var parts = inp.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new CommandTriggerInput(parts[0], parts.Skip(1).ToArray());
        }

        public ITriggerPressCollection Press() {
            return CommandTriggerPressCollection.From(Key
                .SelectMany(pair => pair.Key
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(key => (key: key, shortcut: pair.Value)))
                .ToDictionary(
                    pair => PressGesture.Parse(pair.key),
                    pair => pair.shortcut.Replace(
                        "*",
                        Inp?.Split('|')?.FirstOrDefault()?.Trim() ?? "*")));
        }
    }
}
