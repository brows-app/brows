using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brows.Commands {
    internal sealed class DrawImageFrame {
        private Size Scale {
            get {
                if (_Scale == default) {
                    var param = Parameter;
                    var width = param.Width;
                    var height = param.Height;
                    if (height.HasValue == false && width.HasValue == false) {
                        return _Scale = new Size(1, 1);
                    }
                    if (height.HasValue && width.HasValue) {
                        return _Scale = new Size(width.Value / (double)SourceWidth, height.Value / (double)SourceHeight);
                    }
                    var w = default(double);
                    var h = default(double);
                    if (height.HasValue == false && width.HasValue) {
                        w = (double)width.Value;
                        h = SourceHeight / (double)SourceWidth * w;
                    }
                    if (width.HasValue == false && height.HasValue) {
                        h = (double)height.Value;
                        w = SourceWidth / (double)SourceHeight * h;
                    }
                    _Scale = new Size(w / SourceWidth, h / SourceHeight);
                }
                return _Scale;
            }
        }
        private Size _Scale;

        private TransformGroup Transform {
            get {
                if (_Transform == null) {
                    var transform = _Transform = new TransformGroup();
                    var rotate = OutputRotation;
                    if (rotate != 0) {
                        transform.Children.Add(new RotateTransform(rotate));
                    }
                    transform.Children.Add(new ScaleTransform(Scale.Width, Scale.Height));
                }
                return _Transform;
            }
        }
        private TransformGroup _Transform;

        private bool DecoderIsEncoder =>
            _DecoderIsEncoder ?? (
            _DecoderIsEncoder = Encoder.CodecInfo.ContainerFormat == Source.Decoder.CodecInfo.ContainerFormat).Value;
        private bool? _DecoderIsEncoder;

        private double SourceRotation =>
            _SourceRotation ?? (
            _SourceRotation = DecoderIsEncoder
                ? 0
                : DrawImageRotation.Angle(Source)).Value;
        private double? _SourceRotation;

        private double OutputRotation =>
            _OutputRotation ?? (
            _OutputRotation = SourceRotation + (Parameter.Rotate ?? 0)).Value;
        private double? _OutputRotation;

        private TransformedBitmap TransformedBitmap =>
            _TransformedBitmap ?? (
            _TransformedBitmap = new(Source, Transform));
        private TransformedBitmap _TransformedBitmap;

        public int SourceWidth =>
            _SourceWidth ?? (
            _SourceWidth = Source.PixelWidth).Value;
        private int? _SourceWidth;

        public int SourceHeight =>
            _SourceHeight ?? (
            _SourceHeight = Source.PixelHeight).Value;
        private int? _SourceHeight;

        public double ScaleWidth =>
            Scale.Width;

        public double ScaleHeight =>
            Scale.Height;

        public BitmapFrame Output =>
            _Output ?? (
            _Output = BitmapFrame.Create(TransformedBitmap, Source.Thumbnail, Source.Metadata as BitmapMetadata, Source.ColorContexts));
        private BitmapFrame _Output;

        public BitmapFrame Source { get; }
        public BitmapEncoder Encoder { get; }
        public DrawImageParameter Parameter { get; }

        public DrawImageFrame(BitmapFrame source, BitmapEncoder encoder, DrawImageParameter parameter) {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }
    }
}
