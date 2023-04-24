using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Composition;
    using Threading.Tasks;

    public static class Imports {
        private static TaskCache<IImport> Cache =>
            _Cache ?? (
            _Cache = new(async token => {
                var exports = await Host.Current.Load(token);
                var
                import = new Import(exports);
                import.Populate();
                return import;
            }));
        private static TaskCache<IImport> _Cache;

        public static ValueTask<IImport> Ready(CancellationToken token) {
            return Cache.Ready(token);
        }
    }
}
