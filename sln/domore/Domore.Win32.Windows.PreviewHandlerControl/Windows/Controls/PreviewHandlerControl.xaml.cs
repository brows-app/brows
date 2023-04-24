using System;
using System.Threading;
using System.Windows;

namespace Domore.Windows.Controls {
    using Logs;
    using Runtime.InteropServices;
    using Runtime.Win32;
    using System.Threading.Tasks;

    partial class PreviewHandlerControl {
        private static readonly ILog Log = Logging.For(typeof(PreviewHandlerControl));

        private PreviewWorker Worker;
        private CancellationTokenSource CancellationTokenSource;

        private void TryCancel() {
            var cancellationTokenSource = CancellationTokenSource;
            if (cancellationTokenSource != null) {
                try {
                    cancellationTokenSource.Cancel();
                }
                catch {
                }
            }
        }

        private static readonly DependencyPropertyKey PreviewHandlerSuccessPropertyKey = DependencyProperty.RegisterReadOnly(
            name: nameof(PreviewHandlerSuccess),
            propertyType: typeof(bool),
            ownerType: typeof(PreviewHandlerControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: false,
                propertyChangedCallback: OnPreviewHandlerSuccessChanged));

        private static readonly DependencyPropertyKey PreviewHandlerLoadingPropertyKey = DependencyProperty.RegisterReadOnly(
            name: nameof(PreviewHandlerLoading),
            propertyType: typeof(bool),
            ownerType: typeof(PreviewHandlerControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: false,
                propertyChangedCallback: OnPreviewHandlerLoadingChanged));

        private static DependencyProperty PreviewHandlerSuccessProperty =>
            PreviewHandlerSuccessPropertyKey.DependencyProperty;

        private static DependencyProperty PreviewHandlerLoadingProperty =>
            PreviewHandlerLoadingPropertyKey.DependencyProperty;

        private void Worker_CLSIDChanged(object sender, PreviewWorkerCLSIDChangedEventArgs e) {
            if (e != null) {
                var a = new PreviewHandlerCLSIDChangedEventArgs(e.Extension, e.CLSID, PreviewHandlerCLSIDChangedEvent);
                RaiseEvent(a);
                e.Override = a.Override;
            }
        }

        private async void This_Unloaded(object sender, RoutedEventArgs e) {
            var
            worker = Worker;
            Worker = null;
            if (worker != null) {
                worker.CLSIDChanged -= Worker_CLSIDChanged;
                try {
                    await worker.Unload(CancellationToken.None);
                }
                catch (Exception ex) {
                    if (Log.Info()) {
                        Log.Info(ex);
                    }
                }
            }
            TryCancel();
        }

        private void This_Loaded(object sender, RoutedEventArgs e) {
            Worker = new PreviewWorker();
            Worker.CLSIDChanged += Worker_CLSIDChanged;
        }

        private RECT? Rect() {
            var host = HwndHost;
            var source = PresentationSource.FromVisual(host);
            if (source == null) {
                return null;
            }
            var transform = source.CompositionTarget.TransformToDevice;
            var physical = (Size)transform.Transform((Vector)host.RenderSize);
            return new RECT {
                left = 0,
                top = 0,
                right = (int)physical.Width,
                bottom = (int)physical.Height
            };
        }

        private async void Refresh() {
            TryCancel();
            if (PreviewHandlerEnabled) {
                var source = PreviewHandlerSource; await Task.Delay(250);
                if (source != PreviewHandlerSource) {
                    return;
                }
                var worker = Worker;
                if (worker != null) {
                    using (var cancellationTokenSource = CancellationTokenSource = new()) {
                        try {
                            PreviewHandlerLoading = true;
                            PreviewHandlerSuccess = false;
                            PreviewHandlerSuccess = await worker.Start(
                                file: source,
                                hwnd: HwndHost?.Handle,
                                rect: Rect(),
                                cancellationToken: cancellationTokenSource.Token);
                        }
                        catch (Exception ex) {
                            if (Log.Info()) {
                                Log.Info(ex);
                            }
                        }
                        finally {
                            PreviewHandlerLoading = false;
                            CancellationTokenSource = null;
                        }
                    }
                }
            }
        }

        private void HwndHost_Loaded(object sender, RoutedEventArgs e) {
            Refresh();
        }

        private async void HwndHost_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (PreviewHandlerEnabled) {
                var worker = Worker;
                if (worker != null) {
                    try {
                        await worker.Change(Rect(), CancellationToken.None);
                    }
                    catch (Exception ex) {
                        if (Log.Info()) {
                            Log.Info(ex);
                        }
                    }
                }
            }
        }

        private void OnPreviewHandlerSourceChanged(DependencyPropertyChangedEventArgs e) {
            Refresh();
        }

        private void OnPreviewHandlerRefreshChanged(DependencyPropertyChangedEventArgs e) {
            if (PreviewHandlerRefresh) {
                PreviewHandlerRefresh = false;
                Refresh();
            }
        }

        private void OnPreviewHandlerEnabledChanged(DependencyPropertyChangedEventArgs e) {
            if (PreviewHandlerEnabled == false) {
                PreviewHandlerSuccess = false;
                TryCancel();
            }
        }

        private void OnPreviewHandlerLoadingChanged(DependencyPropertyChangedEventArgs e) {
            var a = new RoutedEventArgs(PreviewHandlerLoadingChangedEvent);
            RaiseEvent(a);
        }

        private void OnPreviewHandlerSuccessChanged(DependencyPropertyChangedEventArgs e) {
            var a = new RoutedEventArgs(PreviewHandlerSuccessChangedEvent);
            RaiseEvent(a);
        }

        private static void OnPreviewHandlerSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewHandlerControl;
            if (c != null) {
                c.OnPreviewHandlerSourceChanged(e);
            }
        }

        private static void OnPreviewHandlerRefreshChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewHandlerControl;
            if (c != null) {
                c.OnPreviewHandlerRefreshChanged(e);
            }
        }

        private static void OnPreviewHandlerEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewHandlerControl;
            if (c != null) {
                c.OnPreviewHandlerEnabledChanged(e);
            }
        }

        private static void OnPreviewHandlerSuccessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewHandlerControl;
            if (c != null) {
                c.OnPreviewHandlerSuccessChanged(e);
            }
        }

        private static void OnPreviewHandlerLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as PreviewHandlerControl;
            if (c != null) {
                c.OnPreviewHandlerLoadingChanged(e);
            }
        }

        public static readonly RoutedEvent PreviewHandlerCLSIDChangedEvent = EventManager.RegisterRoutedEvent(
            name: nameof(PreviewHandlerCLSIDChanged),
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(PreviewHandlerCLSIDChangedEventHandler),
            ownerType: typeof(PreviewHandlerControl));

        public static readonly RoutedEvent PreviewHandlerSuccessChangedEvent = EventManager.RegisterRoutedEvent(
            name: nameof(PreviewHandlerSuccessChanged),
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(RoutedEventHandler),
            ownerType: typeof(PreviewHandlerControl));

        public static readonly RoutedEvent PreviewHandlerLoadingChangedEvent = EventManager.RegisterRoutedEvent(
            name: nameof(PreviewHandlerLoadingChanged),
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(RoutedEventHandler),
            ownerType: typeof(PreviewHandlerControl));

        public static readonly DependencyProperty PreviewHandlerSourceProperty = DependencyProperty.Register(
            name: nameof(PreviewHandlerSource),
            propertyType: typeof(string),
            ownerType: typeof(PreviewHandlerControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: default(string),
                propertyChangedCallback: OnPreviewHandlerSourceChanged));

        public static readonly DependencyProperty PreviewHandlerRefreshProperty = DependencyProperty.Register(
            name: nameof(PreviewHandlerRefresh),
            propertyType: typeof(bool),
            ownerType: typeof(PreviewHandlerControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: false,
                propertyChangedCallback: OnPreviewHandlerRefreshChanged));

        public static readonly DependencyProperty PreviewHandlerEnabledProperty = DependencyProperty.Register(
            name: nameof(PreviewHandlerEnabled),
            propertyType: typeof(bool),
            ownerType: typeof(PreviewHandlerControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: true,
                propertyChangedCallback: OnPreviewHandlerEnabledChanged));

        public event PreviewHandlerCLSIDChangedEventHandler PreviewHandlerCLSIDChanged {
            add => AddHandler(PreviewHandlerCLSIDChangedEvent, value);
            remove => RemoveHandler(PreviewHandlerCLSIDChangedEvent, value);
        }

        public event RoutedEventHandler PreviewHandlerSuccessChanged {
            add => AddHandler(PreviewHandlerSuccessChangedEvent, value);
            remove => RemoveHandler(PreviewHandlerSuccessChangedEvent, value);
        }

        public event RoutedEventHandler PreviewHandlerLoadingChanged {
            add => AddHandler(PreviewHandlerLoadingChangedEvent, value);
            remove => RemoveHandler(PreviewHandlerLoadingChangedEvent, value);
        }

        public string PreviewHandlerSource {
            get => GetValue(PreviewHandlerSourceProperty) as string;
            set => SetValue(PreviewHandlerSourceProperty, value);
        }

        public bool PreviewHandlerSuccess {
            get => GetValue(PreviewHandlerSuccessProperty) as bool? ?? false;
            private set { SetValue(PreviewHandlerSuccessPropertyKey, value); }
        }

        public bool PreviewHandlerLoading {
            get => GetValue(PreviewHandlerLoadingProperty) as bool? ?? false;
            private set { SetValue(PreviewHandlerLoadingPropertyKey, value); }
        }

        public bool PreviewHandlerRefresh {
            get => GetValue(PreviewHandlerRefreshProperty) as bool? ?? false;
            set => SetValue(PreviewHandlerRefreshProperty, value);
        }

        public bool PreviewHandlerEnabled {
            get => GetValue(PreviewHandlerEnabledProperty) as bool? ?? true;
            set => SetValue(PreviewHandlerEnabledProperty, value);
        }

        public PreviewHandlerControl() {
            Loaded += This_Loaded;
            Unloaded += This_Unloaded;
            InitializeComponent();
        }
    }
}
