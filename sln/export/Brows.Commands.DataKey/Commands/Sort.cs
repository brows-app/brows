using Brows.Commands.DataKey;
using System.Linq;

namespace Brows.Commands {
    internal sealed class Sort : DataKeyCommand<Sort.Parameter> {
        protected sealed override string Parse(string arg) {
            return arg == "!"
                ? null
                : arg?.TrimEnd('<', '>');
        }

        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (context.HasParameter(out var parameter) == false) return false;
            if (active.HasView(out var view) == false) {
                return false;
            }
            var workable = Workable(context);
            if (workable == false) {
                return false;
            }
            var sorting = parameter.Clear ? null : parameter.Sorting;
            var sorted = view.Sort(sorting);
            if (sorted == null || sorted.Any(s => s != null)) {
                return true;
            }
            return false;
        }

        public sealed class Parameter : DataKeyCommandParameter {
            public bool Clear => Args.Count == 1 && Args[0] == "!";

            public IEntrySorting Sorting =>
                _Sorting ?? (
                _Sorting = EntrySorting.From(Args
                    .Select(arg => (
                        Key: arg.TrimEnd('<', '>'),
                        Dir:
                            arg.EndsWith('<') ? EntrySortDirection.Ascending :
                            arg.EndsWith('>') ? EntrySortDirection.Descending :
                            EntrySortDirection.Ascending
                    ))
                    .ToDictionary(item => item.Key, item => item.Dir)));
            private IEntrySorting _Sorting;
        }
    }
}
