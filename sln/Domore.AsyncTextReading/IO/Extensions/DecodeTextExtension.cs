using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IO.Extensions {
    using Text;

    public static class DecodeTextExtension {
        private static async Task<DecodedText> DecodeText(FileInfo fileInfo, DecodedTextOptions options, Func<DecodedTextOptions, StreamTextDecoder> decoder, CancellationToken cancellationToken) {
            if (null == decoder) throw new ArgumentNullException(nameof(decoder));
            if (null == fileInfo) throw new ArgumentNullException(nameof(fileInfo));
            var opt = options ?? new();
            var dec = decoder(opt);
            await using (var stream = fileInfo.OpenRead()) {
                return await dec.Decode(stream, cancellationToken);
            }
        }

        public static Task<DecodedText> DecodeText(this FileInfo fileInfo, DecodedTextDelegate decoded, DecodedTextOptions options, CancellationToken cancellationToken) {
            return DecodeText(fileInfo, options, opt => opt.ForStream(decoded), cancellationToken);
        }

        public static Task<DecodedText> DecodeText(this FileInfo fileInfo, DecodedTextBuilder builder, DecodedTextOptions options, CancellationToken cancellationToken) {
            return DecodeText(fileInfo, options, opt => opt.ForStream(builder), cancellationToken);
        }
    }
}
