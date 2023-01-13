using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using DataKey;

    internal class Show : DataKeyCommand<Show.Parameter>, ICommandExport {
        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasEntries(out var entries)) {
                if (context.HasParameter(out var param)) {
                    if (param.Clear) {
                        entries.ClearColumns();
                    }
                    var cols = param.List.ToArray();
                    var added = cols.Length > 0 ? entries.AddColumns(cols) : Array.Empty<string>();
                    var success = added.Where(a => a != null).Any();
                    if (success == false) {
                        entries.RefreshColumns();
                    }
                    return await Worked;
                }
            }
            return false;
        }

        public class Parameter : DataKeyCommandParameter {
            [Switch(Required = false, Name = "clear", ShortName = '!')]
            public bool Clear { get; set; }
        }
    }
}
