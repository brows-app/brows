using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IO {
    using Extensions;
    using Logs;
    using Text;

    internal class PreviewTextWorker {
        private static readonly ILog Log = Logging.For(typeof(PreviewTextWorker));

        private async Task<DecodedText> Work(DecodedTextBuilder builder, CancellationToken cancellationToken) {
            if (Enabled == false) {
                return null;
            }
            var filePath = FilePath;
            if (filePath == null || filePath == "") {
                return null;
            }
            var options = Options;
            var fileLengthMax = FileLengthMax;
            try {
                return await Task.Run(cancellationToken: cancellationToken, function: async () => {
                    var file = new FileInfo(filePath);
                    if (file.Exists == false) {
                        return null;
                    }
                    if (fileLengthMax.HasValue) {
                        var length = file.Length;
                        if (length > fileLengthMax.Value) {
                            return null;
                        }
                    }
                    return await file.DecodeText(builder, options, cancellationToken);
                });
            }
            catch (Exception ex) {
                if (ex is OperationCanceledException canceled && canceled.CancellationToken == cancellationToken) {
                    if (Log.Info()) {
                        Log.Info($"{nameof(canceled)}[{filePath}]");
                    }
                }
                else {
                    if (Log.Info()) {
                        Log.Info($"{nameof(Exception)}[{filePath}]", ex);
                    }
                }
                return null;
            }
        }

        public bool Enabled { get; set; }
        public string FilePath { get; set; }
        public long? FileLengthMax { get; set; }
        public DecodedTextOptions Options { get; set; }
        public DecodedText Decoded { get; private set; }

        public async Task Refresh(DecodedTextBuilder builder, CancellationToken cancellationToken) {
            Decoded = await Work(builder, cancellationToken);
        }
    }
}
