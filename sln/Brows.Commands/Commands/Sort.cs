using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Sort : Keyed<Sort.Info>, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("sort");
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(Context context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasPanel(out var active)) {
                if (context.HasParameter(out var parameter)) {
                    var sorting = parameter.Parse();
                    var sorted = active.Entries.SortColumns(sorting);
                    if (sorted[0] != null) {
                        return true;
                    }
                }
            }
            await Task.CompletedTask;
            return false;
        }

        public class Info {
            [Argument(Name = "key")]
            public string Key { get; set; }

            public IReadOnlyDictionary<string, EntrySortDirection?> Parse() {
                return new Dictionary<string, EntrySortDirection?> {
                    { Key, EntrySortDirection.Ascending }
                };
            }
        }
    }
}
