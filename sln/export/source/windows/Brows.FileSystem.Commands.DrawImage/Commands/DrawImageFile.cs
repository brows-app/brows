using Brows.Win32;
using Domore.Logs;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Brows.Commands;

internal sealed class DrawImageFile : IDisposable {
    private static readonly ILog Log = Logging.For(typeof(DrawImageFile));

    private IReadOnlyList<DrawImageFrame> Frames => field ??=
        [.. Decoder.Frames.Select(frame => new DrawImageFrame(frame, Parameter))];

    /*
     * Only TIFF output keeps every frame, as pages. Other formats either hold
     * one frame or, like GIF, need animation data that is not reproduced here,
     * so they keep the first frame.
     */
    private IList<BitmapFrame> OutputFrames {
        get {
            var frames = Frames.Select(frame => frame.Output);
            if (Encoder is TiffBitmapEncoder) {
                return [.. frames];
            }
            return [.. frames.Take(1)];
        }
    }

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

    /*
     * The decoder reads from a stream owned here, which is closed as soon as
     * the image is encoded, so the output can replace the source and the
     * source is not held after saving.
     */
    private BitmapDecoder Decoder {
        get {
            if (field is null) {
                ObjectDisposedException.ThrowIf(Disposed, this);
                SourceStream = File.OpenRead();
                field = BitmapDecoder.Create(SourceStream,
                                             BitmapCreateOptions.PreservePixelFormat,
                                             BitmapCacheOption.Default);
            }
            return field;
        }
    }

    private FileStream SourceStream { get; set; }
    private bool Disposed { get; set; }

    private BitmapEncoder Encoder => field ??=
        DrawImageEncoder.For(Path.GetExtension(Output));

    private void CloseSource() {
        SourceStream?.Dispose();
        SourceStream = null;
    }

    private void Encode(string path) {
        var encoder = Encoder;
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            encoder.Save(stream);
        }
    }

    private void Transfer(string destination) {
        if (DecoderCodec.ContainerFormat == EncoderCodec.ContainerFormat) {
            return;
        }
        var list = Parameter.TransferMetadata.Select(name => new PropertyWildcard(name)).ToList();
        if (list.Count == 0) {
            return;
        }
        PropertySystem.TransferPropertyValues(
            sourceFileName: File.FullName,
            destinationFileName: destination,
            ignoreProperty: property => {
                if (Log.Debug()) {
                    Log.Debug(Log.Join(File.Name, property.Key?.CanonicalName, property.Value));
                }
                var name = property.Key?.CanonicalName;
                if (name == null) {
                    return true;
                }
                if (name == DrawImageOrientation.PropertyName) {
                    return true; // The output pixels already include the orientation.
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
                    Log.Info(Log.Join(property.Key?.CanonicalName, property.Value, error, destination));
                }
                switch (error) {
                    case 0x80030005: // HRESULT.STG_E_ACCESSDENIED:
                    case 0x88982F41: // HRESULT.WINCODEC_ERR_PROPERTYNOTSUPPORTED:
                        return true;
                    default:
                        return false;
                }
            });
    }

    public BitmapCodecInfo DecoderCodec => Decoder.CodecInfo;
    public BitmapCodecInfo EncoderCodec => Encoder.CodecInfo;

    public string Output => Targets.Output(File);
    public FileInfo File { get; }
    public DrawImage.Parameter Parameter { get; }
    public DrawImageTargets Targets { get; }

    public DrawImageFile(FileInfo file, DrawImage.Parameter parameter, DrawImageTargets targets) {
        File = file ?? throw new ArgumentNullException(nameof(file));
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        Targets = targets ?? throw new ArgumentNullException(nameof(targets));
    }

    /*
     * Encodes the image into a new temporary file beside the output, then
     * publishes it, so a failed conversion leaves the destination unchanged and
     * a replaced file keeps none of its previous content.
     */
    public string Save() {
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
        encoder.Frames = OutputFrames;
        var temporary = Targets.Temporary(File);
        try {
            try {
                Encode(temporary);
            }
            finally {
                CloseSource();
            }
            Transfer(temporary);
            var output = Targets.Publish(File, temporary);
            if (Log.Info()) {
                Log.Info(Log.Join(File.Name, output));
            }
            return output;
        }
        finally {
            new FileInfo(temporary).Delete();
        }
    }

    public void Dispose() {
        Disposed = true;
        CloseSource();
    }

    private sealed class DrawImageUnknownBitmapEncoderException : Exception {
        public BitmapEncoder UnknownEncoder { get; }

        public DrawImageUnknownBitmapEncoderException(BitmapEncoder unknownEncoder) {
            UnknownEncoder = unknownEncoder;
        }
    }
}
