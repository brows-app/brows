using Domore.IO.Extensions;
using Domore.Text;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Gui {
    using Config;
    using IO;

    public abstract class PreviewProvider : ImageSourceProvider<IPreviewInput>, IPreviewProvider {
        private IConfig<PreviewTextConfig> Config =>
            _Config ?? (
            _Config = Configure.File<PreviewTextConfig>());
        private IConfig<PreviewTextConfig> _Config;

        public virtual async Task<DecodedText> GetPreviewText(IPreviewInput input, DecodedTextBuilder builder, CancellationToken cancellationToken) {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var fileInfo = await FileSystem.FileExists(input.File, cancellationToken);
            if (fileInfo == null) {
                return null;
            }
            var config = await Config.Load(cancellationToken);
            return await fileInfo.DecodeText(builder, config, cancellationToken);
        }
    }
}
