namespace Brows {
    using Windows;

    partial class App {
        internal AppInstance Instance { get; }

        public App() {
            var instance = Instance = new AppInstance(this);
            if (instance.ShuttingDown == false) {
                new AppGlobal(this);
                new AppConfig(this);
                new AppLogger(this);
            }
        }
    }
}
