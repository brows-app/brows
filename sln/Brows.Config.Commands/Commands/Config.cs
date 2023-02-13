using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Brows.Config;

    internal class Config : Command<Config.Parameter>, ICommandExport {
        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            var path = await ConfigPath.FileReady(cancellationToken);
            return await context.OpenOrAddPanel(path, cancellationToken);
        }

        public class Parameter {
        }
    }
}
