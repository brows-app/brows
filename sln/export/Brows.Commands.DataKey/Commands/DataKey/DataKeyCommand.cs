using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands.DataKey {
    internal abstract class DataKeyCommand<T> : Command<T> where T : DataKeyCommandParameter, new() {
        private async IAsyncEnumerable<ICommandSuggestion> Suggest(Context context, IProvider provider, T parameter, [EnumeratorCancellation] CancellationToken token) {
            if (null == context) throw new ArgumentNullException(nameof(context));
            if (null == provider) throw new ArgumentNullException(nameof(provider));
            var data = provider.Data;
            var suggester = new DataKeyCommandSuggester<T>(data.Key, parameter, Parse);
            var suggestedKeys = suggester.Suggest();
            var suggestedCount = 0;
            await foreach (var suggestion in base.Suggest(context, token)) {
                if (suggestion.History) {
                    continue;
                }
                foreach (var key in suggestedKeys) {
                    var definition = data.Get(key);
                    if (definition == null) {
                        continue;
                    }
                    var suggested = definition.SuggestKey(context);
                    if (suggested == false) {
                        continue;
                    }
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
                        group: definition.Group,
                        help: help,
                        input: input,
                        relevance: suggestion.Relevance);
                    var count = ++suggestedCount;
                    if (count > 10) {
                        await Task.Delay(10, token);
                        suggestedCount = 0;
                    }
                }
                context.SetHint(new DataKeyCommandHint(this, suggestion.Help, suggestion.Description));
            }
        }

        protected virtual string Parse(string input) {
            return input;
        }

        protected bool Workable(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (context.HasParameter(out var parameter) == false) return false;
            if (active.HasProvider(out IProvider provider) == false) {
                return false;
            }
            foreach (var arg in parameter.Args) {
                var alias = Parse(arg);
                if (alias != null) {
                    var lookup = provider.Data.Key.Lookup(alias);
                    if (lookup == null) {
                        return false;
                    }
                }
            }
            return true;
        }

        protected override IAsyncEnumerable<ICommandSuggestion> Suggest(Context context, CancellationToken cancellationToken) {
            if (null == context) throw new ArgumentNullException(nameof(context));
            if (context.DidTrigger(this)) {
                if (context.HasPanel(out var active)) {
                    if (active.HasProvider(out IProvider provider)) {
                        if (context.GetParameter(out var parameter)) {
                            return Suggest(context, provider, parameter, cancellationToken);
                        }
                    }
                }
            }
            return base.Suggest(context, cancellationToken);
        }

        public sealed override string ConfText => null;
    }
}
