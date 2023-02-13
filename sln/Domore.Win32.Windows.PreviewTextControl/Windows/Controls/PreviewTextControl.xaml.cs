using System.Threading;
using System.Windows;

namespace Domore.Windows.Controls {
    using IO;
    using Text;

    partial class PreviewTextControl {
        private PreviewTextWorker Worker;
        private CancellationTokenSource Cancellation;

        private static readonly DependencyPropertyKey PreviewTextWorkedPropertyKey = DependencyProperty.RegisterReadOnly(
            name: nameof(PreviewTextWorked),
            propertyType: typeof(bool),
            ownerType: typeof(PreviewTextControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: false,
                propertyChangedCallback: OnPreviewTextWorkedChanged));

        private static readonly DependencyPropertyKey PreviewTextEncodingPropertyKey = DependencyProperty.RegisterReadOnly(
            name: nameof(PreviewTextEncoding),
            propertyType: typeof(string),
            ownerType: typeof(PreviewTextControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: null,
                propertyChangedCallback: OnPreviewTextEncodingChanged));

        private static DependencyProperty PreviewTextWorkedProperty =>
            PreviewTextWorkedPropertyKey.DependencyProperty;

        private static DependencyProperty PreviewTextEncodingProperty =>
            PreviewTextEncodingPropertyKey.DependencyProperty;

        private void This_Unloaded(object sender, RoutedEventArgs e) {
            Worker = null;
            var cancellation = Cancellation;
            if (cancellation != null) {
                try {
                    cancellation.Cancel();
                }
                catch {
                }
            }
        }

        private void This_Loaded(object sender, RoutedEventArgs e) {
            Worker = new PreviewTextWorker {
                Enabled = PreviewTextEnabled,
                FilePath = PreviewTextSource,
                FileLengthMax = PreviewTextFileLengthMax,
                Options = PreviewTextOptions
            };
        }

        private async void Refresh() {
            var cancellation = Cancellation;
            if (cancellation != null) {
                try {
                    cancellation.Cancel();
                }
                catch {
                }
            }
            var worker = Worker;
            if (worker == null) {
                return;
            }
            using (var c = Cancellation = new CancellationTokenSource()) {
                await worker.Refresh(
                    builder: new PreviewTextBuilder(this),
                    cancellationToken: c.Token);
                Cancellation = null;
                PreviewTextWorked = worker.Decoded?.Success == true;
                PreviewTextEncoding = worker.Decoded?.Encoding;
            }
        }

        private void OnPreviewTextSourceChanged(DependencyPropertyChangedEventArgs e) {
            var worker = Worker;
            if (worker != null) {
                worker.FilePath = PreviewTextSource;
            }
            var a = new RoutedEventArgs(PreviewTextSourceChangedEvent);
            RaiseEvent(a);
            Refresh();
        }

        private void OnPreviewTextEncodingChanged(DependencyPropertyChangedEventArgs e) {
            var a = new RoutedEventArgs(PreviewTextEncodingChangedEvent);
            RaiseEvent(a);
        }

        private void OnPreviewTextWorkedChanged(DependencyPropertyChangedEventArgs e) {
            var a = new RoutedEventArgs(PreviewTextWorkedChangedEvent);
            RaiseEvent(a);
        }

        private void OnPreviewTextRefreshChanged(DependencyPropertyChangedEventArgs e) {
            if (PreviewTextRefresh) {
                PreviewTextRefresh = false;
                Refresh();
            }
        }

        private void OnPreviewTextOptionsChanged(DependencyPropertyChangedEventArgs e) {
            var worker = Worker;
            if (worker != null) {
                worker.Options = PreviewTextOptions;
            }
        }

        private void OnPreviewTextEnabledChanged(DependencyPropertyChangedEventArgs e) {
            var worker = Worker;
            if (worker != null) {
                worker.Enabled = PreviewTextEnabled;
            }
        }

        private void OnPreviewTextFileLengthMaxChanged(DependencyPropertyChangedEventArgs e) {
            var worker = Worker;
            if (worker != null) {
                worker.FileLengthMax = PreviewTextFileLengthMax;
            }
        }

        private static void OnPreviewTextSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewTextControl;
            if (c != null) {
                c.OnPreviewTextSourceChanged(e);
            }
        }

        private static void OnPreviewTextEncodingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewTextControl;
            if (c != null) {
                c.OnPreviewTextEncodingChanged(e);
            }
        }

        private static void OnPreviewTextWorkedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewTextControl;
            if (c != null) {
                c.OnPreviewTextWorkedChanged(e);
            }
        }

        private static void OnPreviewTextOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewTextControl;
            if (c != null) {
                c.OnPreviewTextOptionsChanged(e);
            }
        }

        private static void OnPreviewTextEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewTextControl;
            if (c != null) {
                c.OnPreviewTextEnabledChanged(e);
            }
        }

        private static void OnPreviewTextRefreshChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewTextControl;
            if (c != null) {
                c.OnPreviewTextRefreshChanged(e);
            }
        }

        private static void OnPreviewTextFileLengthMaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewTextControl;
            if (c != null) {
                c.OnPreviewTextFileLengthMaxChanged(e);
            }
        }

        public static readonly RoutedEvent PreviewTextSourceChangedEvent = EventManager.RegisterRoutedEvent(
            name: nameof(PreviewTextSourceChanged),
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(RoutedEventHandler),
            ownerType: typeof(PreviewTextControl));

        public static readonly RoutedEvent PreviewTextWorkedChangedEvent = EventManager.RegisterRoutedEvent(
            name: nameof(PreviewTextWorkedChanged),
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(RoutedEventHandler),
            ownerType: typeof(PreviewTextControl));

        public static readonly RoutedEvent PreviewTextEncodingChangedEvent = EventManager.RegisterRoutedEvent(
            name: nameof(PreviewTextEncodingChanged),
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(RoutedEventHandler),
            ownerType: typeof(PreviewTextControl));

        public static readonly DependencyProperty PreviewTextSourceProperty = DependencyProperty.Register(
            name: nameof(PreviewTextSource),
            propertyType: typeof(string),
            ownerType: typeof(PreviewTextControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: null,
                propertyChangedCallback: OnPreviewTextSourceChanged));

        public static readonly DependencyProperty PreviewTextEnabledProperty = DependencyProperty.Register(
            name: nameof(PreviewTextEnabled),
            propertyType: typeof(bool),
            ownerType: typeof(PreviewTextControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: true,
                propertyChangedCallback: OnPreviewTextEnabledChanged));

        public static readonly DependencyProperty PreviewTextRefreshProperty = DependencyProperty.Register(
            name: nameof(PreviewTextRefresh),
            propertyType: typeof(bool),
            ownerType: typeof(PreviewTextControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: false,
                propertyChangedCallback: OnPreviewTextRefreshChanged));

        public static readonly DependencyProperty PreviewTextFileLengthMaxProperty = DependencyProperty.Register(
            name: nameof(PreviewTextFileLengthMax),
            propertyType: typeof(long?),
            ownerType: typeof(PreviewTextControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: default(long?),
                propertyChangedCallback: OnPreviewTextFileLengthMaxChanged));

        public static readonly DependencyProperty PreviewTextOptionsProperty = DependencyProperty.Register(
            name: nameof(PreviewTextOptions),
            propertyType: typeof(DecodedTextOptions),
            ownerType: typeof(PreviewTextControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: null,
                propertyChangedCallback: OnPreviewTextOptionsChanged));

        public event RoutedEventHandler PreviewTextSourceChanged {
            add => AddHandler(PreviewTextSourceChangedEvent, value);
            remove => RemoveHandler(PreviewTextSourceChangedEvent, value);
        }

        public event RoutedEventHandler PreviewTextWorkedChanged {
            add => AddHandler(PreviewTextWorkedChangedEvent, value);
            remove => RemoveHandler(PreviewTextWorkedChangedEvent, value);
        }

        public event RoutedEventHandler PreviewTextEncodingChanged {
            add => AddHandler(PreviewTextEncodingChangedEvent, value);
            remove => RemoveHandler(PreviewTextEncodingChangedEvent, value);
        }

        public string PreviewTextSource {
            get => GetValue(PreviewTextSourceProperty) as string;
            set => SetValue(PreviewTextSourceProperty, value);
        }

        public bool PreviewTextEnabled {
            get => GetValue(PreviewTextEnabledProperty) as bool? ?? true;
            set => SetValue(PreviewTextEnabledProperty, value);
        }

        public bool PreviewTextRefresh {
            get => GetValue(PreviewTextRefreshProperty) as bool? ?? false;
            set => SetValue(PreviewTextRefreshProperty, value);
        }

        public long? PreviewTextFileLengthMax {
            get => GetValue(PreviewTextFileLengthMaxProperty) as long?;
            set => SetValue(PreviewTextFileLengthMaxProperty, value);
        }

        public DecodedTextOptions PreviewTextOptions {
            get => GetValue(PreviewTextOptionsProperty) as DecodedTextOptions;
            set => SetValue(PreviewTextOptionsProperty, value);
        }

        public bool PreviewTextWorked {
            get => GetValue(PreviewTextWorkedProperty) as bool? ?? false;
            private set => SetValue(PreviewTextWorkedPropertyKey, value);
        }

        public string PreviewTextEncoding {
            get => GetValue(PreviewTextEncodingProperty) as string;
            private set => SetValue(PreviewTextEncodingPropertyKey, value);
        }

        public PreviewTextControl() {
            Loaded += This_Loaded;
            Unloaded += This_Unloaded;
            InitializeComponent();
        }
    }
}
