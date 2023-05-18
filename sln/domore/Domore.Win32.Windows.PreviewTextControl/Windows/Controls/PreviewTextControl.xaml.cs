using Domore.IO;
using Domore.Text;
using System.Threading;
using System.Windows;

namespace Domore.Windows.Controls {
    partial class PreviewTextControl {
        private PreviewTextWorker Worker;
        private CancellationTokenSource Cancellation;

        private static readonly DependencyPropertyKey PreviewTextSuccessPropertyKey = DependencyProperty.RegisterReadOnly(
            name: nameof(PreviewTextSuccess),
            propertyType: typeof(bool),
            ownerType: typeof(PreviewTextControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: false,
                propertyChangedCallback: OnPreviewTextSuccessChanged));

        private static readonly DependencyPropertyKey PreviewTextLoadingPropertyKey = DependencyProperty.RegisterReadOnly(
            name: nameof(PreviewTextLoading),
            propertyType: typeof(bool),
            ownerType: typeof(PreviewTextControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: false,
                propertyChangedCallback: OnPreviewTextLoadingChanged));

        private static readonly DependencyPropertyKey PreviewTextEncodingPropertyKey = DependencyProperty.RegisterReadOnly(
            name: nameof(PreviewTextEncoding),
            propertyType: typeof(string),
            ownerType: typeof(PreviewTextControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: null,
                propertyChangedCallback: OnPreviewTextEncodingChanged));

        private static DependencyProperty PreviewTextSuccessProperty =>
            PreviewTextSuccessPropertyKey.DependencyProperty;

        private static DependencyProperty PreviewTextLoadingProperty =>
            PreviewTextLoadingPropertyKey.DependencyProperty;

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
                Source = PreviewTextSource,
                SourceLengthMax = PreviewTextSourceLengthMax,
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
                PreviewTextLoading = true;
                PreviewTextSuccess = false;
                await worker.Refresh(
                    builder: new PreviewTextBuilder(this),
                    cancellationToken: c.Token);
                Cancellation = null;
                PreviewTextSuccess = worker.Decoded?.Success == true;
                PreviewTextEncoding = worker.Decoded?.Encoding;
                PreviewTextLoading = false;
            }
        }

        private void OnPreviewTextSourceChanged(DependencyPropertyChangedEventArgs e) {
            var worker = Worker;
            if (worker != null) {
                worker.Source = PreviewTextSource;
            }
            var a = new RoutedEventArgs(PreviewTextSourceChangedEvent);
            RaiseEvent(a);
            Refresh();
        }

        private void OnPreviewTextEncodingChanged(DependencyPropertyChangedEventArgs e) {
            var a = new RoutedEventArgs(PreviewTextEncodingChangedEvent);
            RaiseEvent(a);
        }

        private void OnPreviewTextSuccessChanged(DependencyPropertyChangedEventArgs e) {
            var a = new RoutedEventArgs(PreviewTextSuccessChangedEvent);
            RaiseEvent(a);
        }

        private void OnPreviewTextLoadingChanged(DependencyPropertyChangedEventArgs e) {
            var a = new RoutedEventArgs(PreviewTextLoadingChangedEvent);
            RaiseEvent(a);
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

        private void OnPreviewTextSourceLengthMaxChanged(DependencyPropertyChangedEventArgs e) {
            var worker = Worker;
            if (worker != null) {
                worker.SourceLengthMax = PreviewTextSourceLengthMax;
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

        private static void OnPreviewTextSuccessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewTextControl;
            if (c != null) {
                c.OnPreviewTextSuccessChanged(e);
            }
        }

        private static void OnPreviewTextLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewTextControl;
            if (c != null) {
                c.OnPreviewTextLoadingChanged(e);
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

        private static void OnPreviewTextSourceLengthMaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewTextControl;
            if (c != null) {
                c.OnPreviewTextSourceLengthMaxChanged(e);
            }
        }

        public static readonly RoutedEvent PreviewTextSourceChangedEvent = EventManager.RegisterRoutedEvent(
            name: nameof(PreviewTextSourceChanged),
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(RoutedEventHandler),
            ownerType: typeof(PreviewTextControl));

        public static readonly RoutedEvent PreviewTextSuccessChangedEvent = EventManager.RegisterRoutedEvent(
            name: nameof(PreviewTextSuccessChanged),
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(RoutedEventHandler),
            ownerType: typeof(PreviewTextControl));

        public static readonly RoutedEvent PreviewTextLoadingChangedEvent = EventManager.RegisterRoutedEvent(
            name: nameof(PreviewTextLoadingChanged),
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
            propertyType: typeof(IStreamText),
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

        public static readonly DependencyProperty PreviewTextSourceLengthMaxProperty = DependencyProperty.Register(
            name: nameof(PreviewTextSourceLengthMax),
            propertyType: typeof(long?),
            ownerType: typeof(PreviewTextControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: default(long?),
                propertyChangedCallback: OnPreviewTextSourceLengthMaxChanged));

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

        public event RoutedEventHandler PreviewTextSuccessChanged {
            add => AddHandler(PreviewTextSuccessChangedEvent, value);
            remove => RemoveHandler(PreviewTextSuccessChangedEvent, value);
        }

        public event RoutedEventHandler PreviewTextLoadingChanged {
            add => AddHandler(PreviewTextLoadingChangedEvent, value);
            remove => RemoveHandler(PreviewTextLoadingChangedEvent, value);
        }

        public event RoutedEventHandler PreviewTextEncodingChanged {
            add => AddHandler(PreviewTextEncodingChangedEvent, value);
            remove => RemoveHandler(PreviewTextEncodingChangedEvent, value);
        }

        public IStreamText PreviewTextSource {
            get => GetValue(PreviewTextSourceProperty) as IStreamText;
            set => SetValue(PreviewTextSourceProperty, value);
        }

        public bool PreviewTextEnabled {
            get => GetValue(PreviewTextEnabledProperty) as bool? ?? true;
            set => SetValue(PreviewTextEnabledProperty, value);
        }

        public long? PreviewTextSourceLengthMax {
            get => GetValue(PreviewTextSourceLengthMaxProperty) as long?;
            set => SetValue(PreviewTextSourceLengthMaxProperty, value);
        }

        public DecodedTextOptions PreviewTextOptions {
            get => GetValue(PreviewTextOptionsProperty) as DecodedTextOptions;
            set => SetValue(PreviewTextOptionsProperty, value);
        }

        public bool PreviewTextSuccess {
            get => GetValue(PreviewTextSuccessProperty) as bool? ?? false;
            private set => SetValue(PreviewTextSuccessPropertyKey, value);
        }

        public bool PreviewTextLoading {
            get => GetValue(PreviewTextLoadingProperty) as bool? ?? false;
            private set => SetValue(PreviewTextLoadingPropertyKey, value);
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
