using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brows.Commands;

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
                    return _Scale = new Size(width.Value / (double)OrientedWidth, height.Value / (double)OrientedHeight);
                }
                var w = default(double);
                var h = default(double);
                if (height.HasValue == false && width.HasValue) {
                    w = (double)width.Value;
                    h = OrientedHeight / (double)OrientedWidth * w;
                }
                if (width.HasValue == false && height.HasValue) {
                    h = (double)height.Value;
                    w = OrientedWidth / (double)OrientedHeight * h;
                }
                _Scale = new Size(w / OrientedWidth, h / OrientedHeight);
            }
            return _Scale;
        }
    }
    private Size _Scale;

    /*
     * The orientation and rotation are applied before the scale, so the
     * requested width and height apply to the axes of the output image.
     */
    private TransformGroup Transform {
        get {
            if (field is null) {
                var transform = field = DrawImageOrientation.Transform(Orientation, Rotate);
                transform.Children.Add(new ScaleTransform(Scale.Width, Scale.Height));
            }
            return field;
        }
    }

    /*
     * Reduced to one turn, so adding the orientation's angle cannot overflow.
     */
    private int Rotate =>
        (Parameter.Rotate ?? 0) % 360;

    private int Orientation =>
        field == 0
            ? field = DrawImageOrientation.Read(Source)
            : field;

    private bool SwapsAxes {
        get {
            var quarterTurns = (DrawImageOrientation.Angle(Orientation) + Rotate) / 90;
            return quarterTurns % 2 != 0;
        }
    }

    private int OrientedWidth =>
        SwapsAxes ? SourceHeight : SourceWidth;

    private int OrientedHeight =>
        SwapsAxes ? SourceWidth : SourceHeight;

    private BitmapSource Thumbnail {
        get {
            var thumbnail = Source.Thumbnail;
            if (thumbnail is null) {
                return null;
            }
            var transform = DrawImageOrientation.Transform(Orientation, Rotate);
            if (transform.Children.Count == 0) {
                return thumbnail;
            }
            return new TransformedBitmap(thumbnail, transform);
        }
    }

    private BitmapMetadata Metadata =>
        Parameter.CopyMetadata == true
            ? DrawImageOrientation.Normalize(Source.Metadata as BitmapMetadata)
            : null;

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
        _Output = BitmapFrame.Create(
            source: TransformedBitmap,
            thumbnail: Thumbnail,
            metadata: Metadata,
            colorContexts: Source.ColorContexts));
    private BitmapFrame _Output;

    public BitmapFrame Source { get; }
    public DrawImage.Parameter Parameter { get; }

    public DrawImageFrame(BitmapFrame source, DrawImage.Parameter parameter) {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
    }
}
