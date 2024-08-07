﻿using Brows.Composition;
using Domore.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public static class Imports {
        private static TaskCache<IImport> Cache =>
            _Cache ?? (
            _Cache = new(async token => {
                var exports = await Host.Current.Load(token).ConfigureAwait(false);
                var
                import = new Import(exports);
                import.Populate();
                return import;
            }));
        private static TaskCache<IImport> _Cache;

        public static Task<IImport> Ready(CancellationToken token) {
            return Cache.Ready(token);
        }
    }
}
