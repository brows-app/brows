using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using DataKey;

    internal class Sort : DataKeyCommand<Sort.Parameter>, ICommandExport {
        protected override string Parse(string arg) {
            return arg.TrimEnd('<', '>');
        }

        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasEntries(out var entries)) {
                if (context.HasParameter(out var param)) {
                    var sorting = param.Clear ? null : param.Sorting;
                    var sorted = entries.SortColumns(sorting);
                    if (sorted == null || sorted.Any(s => s != null)) {
                        return await Worked;
                    }
                }
            }
            return false;
        }

        public class Parameter : DataKeyCommandParameter {
            [Switch(Required = false, Name = "clear", ShortName = '!')]
            public bool Clear { get; set; }

            public IEntrySorting Sorting =>
                _Sorting ?? (
                _Sorting = EntrySorting.From(List
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
