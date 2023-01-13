using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Brows.Commands.DataKey {
    internal abstract class DataKeyCommand<T> : Command<T> where T : DataKeyCommandParameter, new() {
        private async IAsyncEnumerable<ICommandSuggestion> Suggest(Context context, IPanelProvider provider, T parameter, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (null == context) throw new ArgumentNullException(nameof(context));
            var suggester = new DataKeyCommandSuggester<T>(provider, parameter, Parse);
            var suggestedKeys = suggester.Suggest();
            await foreach (var suggestion in base.Suggest(context, cancellationToken)) {
                foreach (var key in suggestedKeys) {
                    var inputSuggestions = suggester.ValidInput.Count == 0
                        ? " "
                        : " " + string.Join(" ", suggester.ValidInput) + " ";
                    var alias = suggester.KeyAlias[key];
                    var first = alias.FirstOrDefault();
                    var other = alias.Skip(1);
                    var input = suggestion.Input + inputSuggestions + (first ?? key);
                    var help = other.Any()
                        ? input + $" ({string.Join(',', other)})"
                        : input;
                    yield return Suggestion(
                        context: context,
                        description: "",
                        help: help,
                        input: input,
                        relevance: suggestion.Relevance);
                }
                context.SetHint(new DataKeyCommandHint(this, suggestion.Help, suggestion.Description));
            }
        }

        protected virtual string Parse(string input) {
            return input;
        }

        protected override bool Workable(Context context) {
            if (context == null) return false;
            if (context.HasProvider(out var provider)) {
                if (context.HasParameter(out var parameter)) {
                    foreach (var arg in parameter.List) {
                        var key = Parse(arg);
                        var lookup = provider.DataKeyLookup(key);
                        if (lookup == null) {
                            return false;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        protected override IAsyncEnumerable<ICommandSuggestion> Suggest(Context context, CancellationToken cancellationToken) {
            if (null == context) throw new ArgumentNullException(nameof(context));
            if (Triggered(context)) {
                if (context.HasProvider(out var provider)) {
                    var parameter = context.HasParameter(out var p)
                        ? p
                        : new T();
                    return Suggest(context, provider, parameter, cancellationToken);
                }
            }
            return base.Suggest(context, cancellationToken);
        }
    }
}
