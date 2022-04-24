using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    public class CommandSuggestionRelevance {
        public int? From(string trigger, string input) {
            if (trigger == null) return null;
            if (input == null) return null;
            var i = trigger.IndexOf(input, StringComparison.CurrentCultureIgnoreCase);
            if (i < 0) return null;
            var d = input.Length - trigger.Length;
            return d;
        }

        public int? From(IEnumerable<string> triggers, string input) {
            if (null == triggers) throw new ArgumentNullException(nameof(triggers));
            return triggers.Min(t => From(t, input));
        }
    }
}
