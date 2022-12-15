using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Hide : Keyed<Hide.Info>, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("hide");
            }
        }

        protected override async Task<bool> WorkAsync(Context context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasPanel(out var active)) {
                if (context.HasParameter(out var parameter)) {
                    var removed = active.Entries.RemoveColumns(parameter.List.ToArray());
                    if (removed[0] != null) {
                        return await Completed;
                    }
                }
            }
            return false;
        }

        public class Info : KeyedInfo {
        }
    }
}
