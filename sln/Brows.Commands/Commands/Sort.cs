using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using System.Linq;
    using Triggers;

    internal class Sort : Keyed<Sort.Info>, ICommandExport {
        protected override string Key(string arg) {
            return arg.TrimEnd('<', '>', '!');
        }

        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("sort");
            }
        }

        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasPanel(out var active)) {
                if (context.HasParameter(out var parameter)) {
                    var sorting = parameter.Sorting;
                    var sorted = active.Entries.SortColumns(sorting);
                    if (sorted.Any(s => s != null)) {
                        return await Worked;
                    }
                }
            }
            return false;
        }

        public class Info : KeyedInfo {
            public IEntrySorting Sorting =>
                _Sorting ?? (
                _Sorting = EntrySorting.From(List
                    .Select(arg => (
                        Key: arg.TrimEnd('<', '>', '!'),
                        Dir:
                            arg.EndsWith('<') ? EntrySortDirection.Ascending :
                            arg.EndsWith('>') ? EntrySortDirection.Descending :
                            arg.EndsWith('!') ? default(EntrySortDirection?) :
                            EntrySortDirection.Ascending
                    ))
                    .ToDictionary(item => item.Key, item => item.Dir)));
            private IEntrySorting _Sorting;
        }
    }
}
