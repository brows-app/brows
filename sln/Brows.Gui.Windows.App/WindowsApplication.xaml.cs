namespace Brows {
    using Windows;

    partial class WindowsApplication {
        public IProgramContext Context { get; set; }

        public AppInstance Instance { get; }

        public WindowsApplication() {
            Instance = new AppInstance(this);
            new AppGlobal(this);
            new AppConfig(this);
            new AppLogger(this);
            InitializeComponent();
        }
    }
}
