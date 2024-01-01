using Domore.Logs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Brows.Extensions {
    internal static class EntryStreamSourceExtension {
        private static readonly ILog Log = Logging.For(typeof(EntryStreamSourceExtension));

        public static async Task<BitmapImage> Image(this IEntryStreamSource entryStreamSource, CancellationToken cancellationToken) {
            if (entryStreamSource is null) throw new ArgumentNullException(nameof(entryStreamSource));
            var entryStreamMemory = await Task.Run(cancellationToken: cancellationToken, function: async () => {
                using (await entryStreamSource.StreamReady(cancellationToken)) {
                    await using (var stream = entryStreamSource.Stream()) {
                        var memory = new MemoryStream();
                        await stream.CopyToAsync(memory, cancellationToken);
                        return memory;
                    }
                }
            });
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = entryStreamMemory;
            image.EndInit();
            if (image.CanFreeze) {
                image.Freeze();
            }
            return image;
        }
    }
}
