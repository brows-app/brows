using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class Bookmarked {
        public string Key { get; set; }
        public string Loc { get; set; }

        public IReadOnlyList<string> Ses {
            get => _Ses ?? (_Ses = new List<string>());
            set => _Ses = value;
        }
        private IReadOnlyList<string> _Ses;

        public async Task<bool> Open(ICommander commander, IPanel active, CancellationToken token) {
            if (null == commander) throw new ArgumentNullException(nameof(commander));
            if (Ses.Count > 0) {
                await commander.ClearPanels(token);
                foreach (var loc in Ses) {
                    await commander.AddPanel(loc, token);
                }
                return true;
            }
            return active != null
                ? await active.Provide(Loc, token)
                : await commander.AddPanel(Loc, token);
        }
    }
}
