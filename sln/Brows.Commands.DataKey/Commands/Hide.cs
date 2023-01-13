using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using DataKey;

    internal class Hide : DataKeyCommand<Hide.Parameter>, ICommandExport {
        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasPanel(out var active)) {
                if (context.HasParameter(out var parameter)) {
                    var removed = active.Entries.RemoveColumns(parameter.List.ToArray());
                    if (removed[0] != null) {
                        return await Worked;
                    }
                }
            }
            return false;
        }

        public class Parameter : DataKeyCommandParameter {
        }
    }
}
