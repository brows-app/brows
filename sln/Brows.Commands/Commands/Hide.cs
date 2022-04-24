using System;
using System.Collections.Generic;
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

        protected override async Task<bool> ProtectedWorkAsync(Context context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasPanel(out var active)) {
                if (context.HasParameter(out var parameter)) {
                    var removed = active.Entries.RemoveColumns(parameter.Key);
                    if (removed[0] != null) {
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
        }
    }
}
