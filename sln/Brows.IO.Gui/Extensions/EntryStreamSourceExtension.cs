using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Brows.Extensions {
    internal static class EntryStreamSourceExtension {
        public static async Task<BitmapImage> Image(this IEntryStreamSource entryStreamSource, CancellationToken token) {
            if (entryStreamSource is null) throw new ArgumentNullException(nameof(entryStreamSource));
            var entryStreamMemory = await Task.Run(cancellationToken: token, function: async () => {
                using (await entryStreamSource.StreamReady(token)) {
                    await using (var stream = entryStreamSource.Stream) {
                        if (stream == null) {
                            return null;
                        }
                        var memory = new MemoryStream();
                        await stream.CopyToAsync(memory, token);
                        return memory;
                    }
                }
            });
            if (entryStreamMemory == null) {
                return null;
            }
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
