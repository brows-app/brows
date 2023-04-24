using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Commands.DataKey {
    internal class DataKeyCommandSuggester<T> where T : DataKeyCommandParameter, new() {
        private IReadOnlyList<string> Input =>
            _Input ?? (
            _Input = Parameter.Args);
        private IReadOnlyList<string> _Input;

        private IReadOnlyDictionary<string, string> ParsedInput =>
            _ParsedInput ?? (
            _ParsedInput = Input
                .Select(input => (input, parsed: Parse(input)))
                .Where(item => item.parsed != null)
                .ToDictionary(item => item.input, item => item.parsed));
        private IReadOnlyDictionary<string, string> _ParsedInput;

        private string LastInput =>
            _LastInput ?? (
            _LastInput = Input.Count > 0
                ? Input[Input.Count - 1]
                : "");
        private string _LastInput;

        private bool LastInputValid =>
            _LastInputValid ?? (
            _LastInputValid = ValidInput.Contains(LastInput)).Value;
        private bool? _LastInputValid;

        private IReadOnlySet<string> LastInputKeyPossibilities =>
            _LastInputKeyPossibilities ?? (
            _LastInputKeyPossibilities = DataKey.Possible(LastInput));
        private IReadOnlySet<string> _LastInputKeyPossibilities;

        private IReadOnlySet<string> KeysInInput =>
            _KeysInInput ?? (
            _KeysInInput = KeyAlias
                .Select(alias => (alias.Key, Input: ParsedInput.Any(input => alias.Value.Contains(input.Value))))
                .Where(alias => alias.Input)
                .Select(alias => alias.Key)
                .ToHashSet());
        private IReadOnlySet<string> _KeysInInput;

        private bool Suggest(string key) =>
            Input.Count == 0 ||
            KeysInInput.Contains(key) == false && (
                LastInput == "" ||
                LastInputValid ||
                LastInputKeyPossibilities.Contains(key));

        public IReadOnlyDictionary<string, IReadOnlySet<string>> KeyAlias =>
            _KeyAlias ?? (
            _KeyAlias = DataKey.Alias());
        private IReadOnlyDictionary<string, IReadOnlySet<string>> _KeyAlias;

        public IReadOnlySet<string> ValidInput =>
            _ValidInput ?? (
            _ValidInput = ParsedInput
                .Where(input => KeyAlias.Values.Any(set => set.Contains(input.Value)))
                .Select(input => input.Key)
                .ToHashSet());
        private IReadOnlySet<string> _ValidInput;

        public IEntryDataKeySet DataKey { get; }
        public T Parameter { get; }
        public Func<string, string> Parse { get; }

        public DataKeyCommandSuggester(IEntryDataKeySet dataKey, T parameter, Func<string, string> parse) {
            Parse = parse ?? throw new ArgumentNullException(nameof(parse));
            DataKey = dataKey ?? throw new ArgumentNullException(nameof(dataKey));
            Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }

        public IEnumerable<string> Suggest() {
            foreach (var key in KeyAlias.Keys) {
                if (Suggest(key)) {
                    yield return key;
                }
            }
        }
    }
}
