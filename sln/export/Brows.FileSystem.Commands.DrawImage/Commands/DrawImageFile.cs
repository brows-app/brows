using Domore.Logs;
using Domore.Runtime.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Brows.Commands {
    internal sealed class DrawImageFile {
        private static readonly ILog Log = Logging.For(typeof(DrawImageFile));

        private IReadOnlyList<DrawImageFrame> Frames =>
            _Frames ?? (
            _Frames = Decoder.Frames.Select(frame => new DrawImageFrame(frame, Encoder, Parameter)).ToList());
        private IReadOnlyList<DrawImageFrame> _Frames;

        public int SourceWidth =>
            _SourceWidth ?? (
            _SourceWidth = Frames.Max(frame => frame.SourceWidth)).Value;
        private int? _SourceWidth;

        public int SourceHeight =>
            _SourceHeight ?? (
            _SourceHeight = Frames.Max(frame => frame.SourceHeight)).Value;
        private int? _SourceHeight;

        public double ScaleWidth =>
            _ScaleWidth ?? (
            _ScaleWidth = Frames.Max(frame => frame.ScaleWidth)).Value;
        private double? _ScaleWidth;

        public double ScaleHeight =>
            _ScaleHeight ?? (
            _ScaleHeight = Frames.Max(frame => frame.ScaleHeight)).Value;
        private double? _ScaleHeight;

        private Uri FileUri =>
            _FileUri ?? (
            _FileUri = new(File.FullName));
        private Uri _FileUri;

        private BitmapDecoder Decoder =>
            _Decoder ?? (
            _Decoder = BitmapDecoder.Create(FileUri, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default));
        private BitmapDecoder _Decoder;

        private BitmapEncoder Encoder =>
            _Encoder ?? (
            _Encoder = DrawImageEncoder.For(Path.GetExtension(Output)));
        private BitmapEncoder _Encoder;

        public BitmapCodecInfo DecoderCodec => Decoder.CodecInfo;
        public BitmapCodecInfo EncoderCodec => Encoder.CodecInfo;

        public string Output { get; }
        public FileInfo File { get; }
        public DrawImageParameter Parameter { get; }

        public DrawImageFile(FileInfo file, DrawImageParameter parameter, string output) {
            Output = output;
            File = file ?? throw new ArgumentNullException(nameof(file));
            Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }

        public void Save() {
            var decoder = Decoder;
            if (Log.Info()) {
                Log.Info(Log.Join(File.Name, nameof(decoder), $"{decoder.CodecInfo.FriendlyName} {decoder.CodecInfo.Version}", $"{SourceWidth}x{SourceHeight}"));
            }
            var encoder = Encoder;
            if (Log.Info()) {
                Log.Info(Log.Join(File.Name, nameof(encoder), $"{encoder.CodecInfo.FriendlyName} {encoder.CodecInfo.Version}", $"{ScaleWidth}x{ScaleHeight}"));
            }
            if (encoder.GetType().Name.Equals("UnknownBitmapEncoder")) {
                throw new DrawImageUnknownBitmapEncoderException(encoder);
            }
            if (encoder is JpegBitmapEncoder jpeg) {
                jpeg.QualityLevel = Parameter.JpegQuality ?? 95;
            }
            encoder.Frames = Frames.Select(frame => frame.Output).Take(1).ToList();
            if (Log.Info()) {
                Log.Info(Log.Join(File.Name, Output));
            }
            using (var stream = new FileInfo(Output).OpenWrite()) {
                encoder.Save(stream);
            }
        }

        public void Transfer() {
            if (DecoderCodec.ContainerFormat == EncoderCodec.ContainerFormat) {
                return;
            }
            var list = Parameter.TransferMetadata.Select(name => new PropertyWildcard(name)).ToList();
            if (list.Count == 0) {
                return;
            }
            PropertySystem.TransferPropertyValues(
                sourceFileName: File.FullName,
                destinationFileName: Output,
                ignoreProperty: property => {
                    if (Log.Debug()) {
                        Log.Debug(Log.Join(File.Name, property.Key?.CanonicalName, property.Value));
                    }
                    var name = property.Key?.CanonicalName;
                    if (name == null) {
                        return true;
                    }
                    var include = list.Any(item => item.Matches(name));
                    var ignore = include == false;
                    if (ignore == false) {
                        ignore = string.IsNullOrWhiteSpace(property.Value?.ToString());
                    }
                    return ignore;
                },
                ignoreError: (property, error) => {
                    if (Log.Info()) {
                        Log.Info(Log.Join(property.Key?.CanonicalName, property.Value, error, Output));
                    }
                    switch (error) {
                        case HRESULT.STG_E_ACCESSDENIED:
                        case HRESULT.WINCODEC_ERR_PROPERTYNOTSUPPORTED:
                            return true;
                        default:
                            return false;
                    }
                });
        }

        private sealed class DrawImageUnknownBitmapEncoderException : Exception {
            public BitmapEncoder UnknownEncoder { get; }

            public DrawImageUnknownBitmapEncoderException(BitmapEncoder unknownEncoder) {
                UnknownEncoder = unknownEncoder;
            }
        }
    }
}
