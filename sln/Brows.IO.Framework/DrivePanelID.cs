using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Translation;

    internal class DrivePanelID : PanelID {
        private ITranslation Translate =>
            Global.Translation;

        protected override bool HasCanonicalValue =>
            true;

        protected override Task<string> GetCanonicalValue(CancellationToken cancellationToken) {
            return Task.FromResult(Translate.Value(nameof(DrivePanelID)));
        }

        public DrivePanelID() : base(nameof(DrivePanelID)) {
        }
    }
}
