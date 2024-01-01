using Domore.Text;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IO.Extensions {
    public static class DecodeTextExtension {
        private static async Task<DecodedText> DecodeText(IStreamText source, DecodedTextOptions options, Func<DecodedTextOptions, StreamTextDecoder> decoder, CancellationToken cancellationToken) {
            if (null == decoder) throw new ArgumentNullException(nameof(decoder));
            if (null == source) throw new ArgumentNullException(nameof(source));
            var opt = options ?? new();
            var dec = decoder(opt);
            var rdy = source.StreamReady(cancellationToken);
            using (rdy == null ? default : await rdy) {
                await using (var stream = source.StreamText()) {
                    return await dec.Decode(stream, cancellationToken);
                }
            }
        }

        public static Task<DecodedText> DecodeText(this IStreamText source, DecodedTextDelegate decoded, DecodedTextOptions options, CancellationToken cancellationToken) {
            return DecodeText(source, options, opt => opt.ForStream(decoded, null), cancellationToken);
        }

        public static Task<DecodedText> DecodeText(this IStreamText source, DecodedTextBuilder builder, DecodedTextOptions options, CancellationToken cancellationToken) {
            return DecodeText(source, options, opt => opt.ForStream(builder), cancellationToken);
        }

        public static Task<DecodedText> DecodeText(this FileInfo fileInfo, DecodedTextDelegate decoded, DecodedTextOptions options, CancellationToken cancellationToken) {
            return DecodeText(new StreamTextSourceFile(fileInfo), options, opt => opt.ForStream(decoded, null), cancellationToken);
        }

        public static Task<DecodedText> DecodeText(this FileInfo fileInfo, DecodedTextBuilder builder, DecodedTextOptions options, CancellationToken cancellationToken) {
            return DecodeText(new StreamTextSourceFile(fileInfo), options, opt => opt.ForStream(builder), cancellationToken);
        }
    }
}
