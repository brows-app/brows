namespace Brows {
    using Windows;

    partial class WindowsApplication {
        public IProgramContext Context { get; set; }

        public AppService Service { get; }

        public WindowsApplication() {
            Service = new AppService(this);
            InitializeComponent();
        }
    }
}
