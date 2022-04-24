using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Brows.Triggers {
    public sealed class InputTrigger : ITrigger {
        private const StringComparison Comparison = StringComparison.CurrentCultureIgnoreCase;

        private readonly ReadOnlyCollection<string> AliasCollection;
        private readonly ReadOnlyCollection<string> AggregateCollection;

        public string Value { get; }
        public string[] Alias => AliasCollection.ToArray();
        public IEnumerable<string> Aggregate => AggregateCollection;

        public InputTrigger(string value, params string[] alias) {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            AliasCollection = new ReadOnlyCollection<string>(new List<string>(alias ?? new string[] { }));
            AggregateCollection = new ReadOnlyCollection<string>(new List<string>(new[] { Value }.Concat(AliasCollection)));
        }

        public bool Triggered(string s) {
            if (Value.Equals(s, Comparison)) return true;
            if (AliasCollection.Any(a => a.Equals(s, Comparison))) return true;
            return false;
        }

        public sealed override bool Equals(object obj) {
            return
                obj is InputTrigger other &&
                other.Value.Equals(Value);
        }

        public sealed override int GetHashCode() {
            return Value.GetHashCode();
        }

        public sealed override string ToString() {
            return Value;
        }
    }
}
