using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    internal class PanelConfig {
        public int AddChunkSize { get; set; } = 1000;
        public int AddChunkDelay { get; set; } = 10;

        public static ValueTask<PanelConfig> Load(CancellationToken cancellationToken) {
            return Configure.File<PanelConfig>().Load(cancellationToken);
        }
    }
}
