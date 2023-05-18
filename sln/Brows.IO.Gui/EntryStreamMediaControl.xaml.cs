using Domore.Logs;
using Domore.Runtime.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brows {
    partial class EntryStreamMediaControl {
        private static readonly ILog Log = Logging.For(typeof(EntryStreamMediaControl));

        private IEntryStreamGui Media;

        private void Button_Click(object sender, RoutedEventArgs e) {
            MediaElement.SpeedRatio = MediaElement.SpeedRatio + 1;
        }

        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e) {
            if (Media == EntryStreamGui) {
                var loop = MediaElement.HasVideo
                    ? Media?.Options?.LoopVideo
                    : MediaElement.HasAudio
                        ? Media?.Options?.LoopAudio
                        : false;
                if (loop == true) {
                    MediaElement.Position = TimeSpan.Zero;
                    MediaElement.Play();
                }
            }
        }

        private void MediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e) {
            if (Media == EntryStreamGui) {
                Media?.State?.Media?.Change(loading: false, success: false);
            }
        }

        private void MediaElement_MediaOpened(object sender, RoutedEventArgs e) {
            if (Media == EntryStreamGui) {
                Media?.State?.Media?.Change(loading: false, success: true);
                var autoplay = MediaElement.HasVideo
                    ? Media?.Options?.AutoplayVideo
                    : MediaElement.HasAudio
                        ? Media?.Options?.AutoplayAudio
                        : false;
                if (autoplay == true) {
                    MediaElement.Play();
                }
                MediaElement.Volume = Media?.Options?.MediaVolume ?? 0.5;
                MediaElement.IsMuted = Media?.Options?.MediaMuted ?? true;
                MediaElement.SpeedRatio = Media?.Options?.MediaSpeedRatio ?? 1;
            }
        }

        private void ChangeEntryStreamGui() {
            MediaElement.Stop();
            MediaElement.Source = null;
            Media = EntryStreamGui;
            Media?.State?.Media?.Change(loading: true, success: false);
            var file = Media?.Source?.SourceFile;
            if (file == null) {
                Media?.State?.Media?.Change(loading: false, success: false);
                return;
            }
            var uri = default(Uri);
            try {
                uri = new Uri(file);
            }
            catch (Exception ex) {
                if (Log.Info()) {
                    Log.Info(ex);
                }
                Media?.State?.Media?.Change(loading: false, success: false);
                return;
            }
            try {
                var video = new Video(file);
                var transform = video.Transform;
                if (transform.Children.Count == 0) {
                    MediaElement.RenderTransformOrigin = new Point(0, 0);
                    MediaElement.RenderTransform = null;
                }
                if (transform.Children.Count > 0) {
                    MediaElement.RenderTransformOrigin = new Point(0.5, 0.5);
                    MediaElement.RenderTransform = transform;
                }
            }
            catch (Exception ex) {
                if (Log.Info()) {
                    Log.Info(ex);
                }
            }
            MediaElement.Source = uri;
        }

        protected sealed override string EntryStreamViewName => "Media";

        protected sealed override void OnEntryStreamGuiChanged(DependencyPropertyChangedEventArgs e) {
            ChangeEntryStreamGui();
            base.OnEntryStreamGuiChanged(e);
        }

        public EntryStreamMediaControl() {
            InitializeComponent();
        }

        private sealed class Video {
            private static PropertyDescription OrientationProperty => _OrientationProperty ??= PropertySystem.GetPropertyDescription("System.Video.Orientation");
            private static PropertyDescription _OrientationProperty;

            private static PropertyDescription FrameHeightProperty => _FrameHeightProperty ??= PropertySystem.GetPropertyDescription("System.Video.FrameHeight");
            private static PropertyDescription _FrameHeightProperty;

            private static PropertyDescription FrameWidthProperty => _FrameWidthProperty ??= PropertySystem.GetPropertyDescription("System.Video.FrameWidth");
            private static PropertyDescription _FrameWidthProperty;

            private string GetProperty(PropertyDescription property) {
                return PropertySystem
                    .GetPropertyValue(File, property, throwOnError: false)?
                    .Object?
                    .ToString();
            }

            private RotateTransform RotateTransform =>
                _RotateTransform ?? (
                _RotateTransform = Orientation == 0
                    ? null
                    : new RotateTransform(angle: Orientation));
            private RotateTransform _RotateTransform;

            private ScaleTransform ScaleTransform {
                get {
                    if (_ScaleTransform == null) {
                        var orientation = Orientation;
                        if (orientation == 90 || orientation == 270) {
                            var h = FrameHeight;
                            var w = FrameWidth;
                            if (w != 0 && h != 0) {
                                var width = (double)h;
                                var height = (double)width / w * h;
                                var scaleX = width / FrameWidth;
                                var scaleY = height / FrameHeight;
                                return _ScaleTransform = new ScaleTransform(scaleX, scaleY);
                            }
                        }
                    }
                    return _ScaleTransform;
                }
            }
            private ScaleTransform _ScaleTransform;

            public int Orientation =>
                _Orientation ?? (
                _Orientation = int.TryParse(GetProperty(OrientationProperty), out var value)
                    ? value
                    : 0).Value;
            private int? _Orientation;

            public int FrameHeight =>
                _FrameHeight ?? (
                _FrameHeight = int.TryParse(GetProperty(FrameHeightProperty), out var value)
                    ? value
                    : 0).Value;
            private int? _FrameHeight;

            public int FrameWidth =>
                _FrameWidth ?? (
                _FrameWidth = int.TryParse(GetProperty(FrameWidthProperty), out var value)
                    ? value
                    : 0).Value;
            private int? _FrameWidth;

            public TransformGroup Transform {
                get {
                    if (_Transform == null) {
                        var group = new TransformGroup();
                        var rotate = RotateTransform;
                        if (rotate != null) {
                            group.Children.Add(rotate);
                        }
                        var scale = ScaleTransform;
                        if (scale != null) {
                            group.Children.Add(scale);
                        }
                        _Transform = group;
                    }
                    return _Transform;
                }
            }
            private TransformGroup _Transform;

            public string File { get; }

            public Video(string file) {
                File = file;
            }
        }
    }
}
