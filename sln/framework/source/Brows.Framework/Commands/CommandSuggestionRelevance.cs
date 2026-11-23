using System.Collections.Generic;
using System.Linq;

namespace Brows.Commands;

/// <summary>
/// Scores how closely command input matches configured trigger names.
/// </summary>
public class CommandSuggestionRelevance {
    /// <summary>
    /// Scores input that occurs within a trigger name, ignoring letter case.
    /// </summary>
    /// <param name="trigger">
    /// The configured input trigger.
    /// </param>
    /// <param name="input">
    /// The input to compare.
    /// </param>
    /// <returns>
    /// The match score, which is zero for a full match and becomes more negative as the input covers less of the
    /// trigger, or null when the input is not found.
    /// </returns>
    public int? From(string trigger, string input) {
        if (trigger == null) return null;
        if (input == null) return null;
        var i = trigger.IndexOf(input, StringComparison.CurrentCultureIgnoreCase);
        if (i < 0) return null;
        var d = input.Length - trigger.Length;
        return d;
    }

    /// <summary>
    /// Finds the lowest available match score among the supplied trigger names.
    /// </summary>
    /// <param name="triggers">
    /// The trigger names to test.
    /// </param>
    /// <param name="input">
    /// The input to compare.
    /// </param>
    /// <returns>
    /// The lowest score, or null when no trigger matches.
    /// </returns>
    public int? From(IEnumerable<string> triggers, string input) {
        if (null == triggers) throw new ArgumentNullException(nameof(triggers));
        return triggers.Min(t => From(t, input));
    }
}
