using Domore.IO.Extensions;
using Domore.Logs;
using Domore.Text;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IO {
    internal class PreviewTextWorker {
        private static readonly ILog Log = Logging.For(typeof(PreviewTextWorker));

        private async Task<DecodedText> Work(DecodedTextBuilder builder, CancellationToken cancellationToken) {
            if (Enabled == false) {
                return null;
            }
            var source = Source;
            if (source == null) {
                return null;
            }
            var options = Options;
            var sourceLengthMax = SourceLengthMax;
            try {
                return await Task.Run(cancellationToken: cancellationToken, function: async () => {
                    if (sourceLengthMax.HasValue) {
                        var length = source.StreamLength;
                        if (length > sourceLengthMax.Value) {
                            return null;
                        }
                    }
                    return await source.DecodeText(builder, options, cancellationToken);
                });
            }
            catch (Exception ex) {
                if (ex is OperationCanceledException canceled && cancellationToken.IsCancellationRequested) {
                    if (Log.Info()) {
                        Log.Info($"{nameof(canceled)}[{source}]");
                    }
                }
                else {
                    if (Log.Info()) {
                        Log.Info($"{nameof(Exception)}[{source}]", ex);
                    }
                }
                return null;
            }
        }

        public bool Enabled { get; set; }
        public long? SourceLengthMax { get; set; }
        public IStreamText Source { get; set; }
        public DecodedTextOptions Options { get; set; }
        public DecodedText Decoded { get; private set; }

        public async Task Refresh(DecodedTextBuilder builder, CancellationToken cancellationToken) {
            Decoded = await Work(builder, cancellationToken);
        }
    }
}
