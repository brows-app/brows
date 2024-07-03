using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Brows.Commands {
    internal static class DrawImageEncoder {
        private static readonly object Locker = new();

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/wic/-wic-guids-clsids#container-formats
        /// </summary>
        private static IReadOnlyDictionary<string, Guid> ContainerFormat => _ContainerFormat ??=
            new Dictionary<string, Guid> {
                { "adng", new Guid(0xf3ff6d0d, 0x38c0, 0x41c4, 0xb1, 0xfe, 0x1f, 0x38, 0x24, 0xf1, 0x7b, 0x84 ) },
                { "bmp", new Guid(0xaf1d87e, 0xfcfe, 0x4188, 0xbd, 0xeb, 0xa7, 0x90, 0x64, 0x71, 0xcb, 0xe3 ) },
                { "png", new Guid(0x1b7cfaf4, 0x713f, 0x473c, 0xbb, 0xcd, 0x61, 0x37, 0x42, 0x5f, 0xae, 0xaf ) },
                { "ico", new Guid(0xa3a860c4, 0x338f, 0x4c17, 0x91, 0x9a, 0xfb, 0xa4, 0xb5, 0x62, 0x8f, 0x21 ) },
                { "jpeg", new Guid(0x19e4a5aa, 0x5662, 0x4fc5, 0xa0, 0xc0, 0x17, 0x58, 0x2, 0x8e, 0x10, 0x57 ) },
                { "tiff", new Guid(0x163bcc30, 0xe2e9, 0x4f0b, 0x96, 0x1d, 0xa3, 0xe9, 0xfd, 0xb7, 0x88, 0xa3 ) },
                { "gif", new Guid(0x1f8a5601, 0x7d4d, 0x4cbd, 0x9c, 0x82, 0x1b, 0xc8, 0xd4, 0xee, 0xb9, 0xa5 ) },
                { "wmp", new Guid(0x57a37caa, 0x367a, 0x4540, 0x91, 0x6b, 0xf1, 0x83, 0xc5, 0x09, 0x3a, 0x4b ) },
                { "heif", new Guid(0xe1e62521, 0x6787, 0x405b, 0xa3, 0x39, 0x50, 0x07, 0x15, 0xb5, 0x76, 0x3f ) },
                { "webp", new Guid(0xe094b0e2, 0x67f2, 0x45b3, 0xb0, 0xea, 0x11, 0x53, 0x37, 0xca, 0x7c, 0xf3) }
            };
        private static IReadOnlyDictionary<string, Guid> _ContainerFormat;

        private static IReadOnlyList<BitmapEncoder> Encoders => _Encoders ??=
            new List<BitmapEncoder>(ContainerFormat.Values
                .Select(guid => BitmapEncoder.Create(guid))
                .Select(encoder => {
                    try { return encoder.CodecInfo == null ? null : encoder; }
                    catch { return null; }
                })
                .Where(encoder => encoder != null));
        private static IReadOnlyList<BitmapEncoder> _Encoders;

        private static IReadOnlyDictionary<string, Guid> Encoder => _Encoder ??=
            Encoders
                .SelectMany(encoder => encoder.CodecInfo.FileExtensions
                    .Split(',')
                    .Select(ext => (ext, encoder)))
                .ToDictionary(
                    item => item.ext,
                    item => item.encoder.CodecInfo.ContainerFormat,
                    StringComparer.OrdinalIgnoreCase);
        private static IReadOnlyDictionary<string, Guid> _Encoder;

        public static BitmapEncoder For(string extension) {
            lock (Locker) {
                if (Encoder.TryGetValue(extension, out var guid)) {
                    return BitmapEncoder.Create(guid);
                }
            }
            throw new ArgumentException(paramName: nameof(extension), message: "No encoder");
        }
    }
}
