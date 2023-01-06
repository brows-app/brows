using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Show : Keyed<Show.Info>, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("show");
            }
        }

        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasPanel(out var active)) {
                if (context.HasParameter(out var parameter)) {
                    var added = active.Entries.AddColumns(parameter.List.ToArray());
                    if (added[0] != null) {
                        return await Worked;
                    }
                }
            }
            return false;
        }

        public class Info : KeyedInfo {
        }
    }
}
