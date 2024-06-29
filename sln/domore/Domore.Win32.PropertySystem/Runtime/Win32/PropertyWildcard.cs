using System;
using System.Collections.Generic;

namespace Domore.Runtime.Win32 {
    public sealed class PropertyWildcard {
        public IReadOnlyList<string> Parts => _Parts ??= Name.Split('.');
        private IReadOnlyList<string> _Parts;

        public string Name { get; }

        public PropertyWildcard(string name) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public bool Matches(string name) {
            if (name == null) {
                return false;
            }
            var other = name.Split('.');
            for (var i = 0; i < Parts.Count; i++) {
                var part = Parts[i];
                if (part == "*") {
                    return true;
                }
                if (other.Length <= i) {
                    return false;
                }
                var thisPart = Parts[i];
                var otherPart = other[i];
                if (otherPart.Equals(thisPart, StringComparison.OrdinalIgnoreCase) == false) {
                    return false;
                }
            }
            return true;
        }
    }
}
