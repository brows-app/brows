using Brows.Commands.DataKey;
using System;
using System.Linq;

namespace Brows.Commands {
    internal sealed class Show : DataKeyCommand<Show.Parameter> {
        protected override string Parse(string input) {
            return input == "!"
                ? null
                : input;
        }

        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasParameter(out var parameter) == false) return false;
            if (context.HasPanel(out var active) == false) {
                return false;
            }
            if (active.HasView(out var view) == false) {
                return false;
            }
            var workable = Workable(context);
            if (workable == false) {
                return false;
            }
            if (parameter.Clear) {
                view.Clear();
            }
            var cols = parameter.Args.ToArray();
            var added = cols.Length > 0 ? view.Add(cols) : Array.Empty<string>();
            var success = added.Where(a => a != null).Any();
            if (success == false) {
                view.Refresh();
            }
            return true;
        }

        public sealed class Parameter : DataKeyCommandParameter {
            public bool Clear => Args.Contains("!");
        }
    }
}
