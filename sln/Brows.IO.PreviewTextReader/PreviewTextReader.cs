using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using IO;
    using Threading.Tasks;

    public class PreviewTextReader {
        private static readonly ILog Log = Logging.For(typeof(PreviewTextReader));

        private async Task<FileStream> TryOpenRead(string path, CancellationToken cancellationToken) {
            try {
                return await Async.Run(cancellationToken, () => File.OpenRead(path));
            }
            catch (Exception ex) {
                if (Log.Info()) {
                    Log.Info(ex);
                }
                return null;
            }
        }

        private async Task<bool> TryFileExists(string path, CancellationToken cancellationToken) {
            try {
                return await FileAsync.Exists(path, cancellationToken);
            }
            catch (Exception ex) {
                if (Log.Info()) {
                    Log.Info(ex);
                }
                return false;
            }
        }

        private async Task<int?> TryReadAsync(FileStream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            try {
                return await stream.ReadAsync(buffer, offset, count, cancellationToken);
            }
            catch (Exception ex) {
                if (Log.Info()) {
                    Log.Info(ex);
                }
                return null;
            }
        }

        private async Task<string> TryGetString(Encoding encoding, byte[] buffer, CancellationToken cancellationToken) {
            return await Async.Run(cancellationToken, () => {
                try {
                    return encoding.GetString(buffer);
                }
                catch {
                    return null;
                }
            });
        }

        public async IAsyncEnumerable<string> GetPreviewText(string path, [EnumeratorCancellation] CancellationToken cancellationToken) {
            var exists = await TryFileExists(path, cancellationToken);
            if (exists == false) {
                yield return null;
                yield break;
            }
            var stream = await TryOpenRead(path, cancellationToken);
            if (stream == null) {
                yield return null;
                yield break;
            }
            await using (stream) {
                var len = 2048;
                var max = 1024000;
                var buffer = new byte[len];
                var contents = new byte[0];
                var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
                for (; ; ) {
                    if (contents.Length > max) {
                        break;
                    }
                    var r = await TryReadAsync(stream, buffer, 0, len, cancellationToken);
                    if (r == null) {
                        yield return null;
                        break;
                    }
                    if (r == 0) {
                        break;
                    }

                    await Async.Run(cancellationToken, () => {
                        var b = new byte[contents.Length + r.Value];
                        Buffer.BlockCopy(contents, 0, b, 0, contents.Length);
                        Buffer.BlockCopy(buffer, 0, b, contents.Length, r.Value);
                        contents = b;
                    });

                    var str = await TryGetString(encoding, contents, cancellationToken);
                    if (str == null) {
                        yield return null;
                        break;
                    }
                    else {
                        yield return str;
                    }
                }
            }
        }
    }
}
