namespace Brows {
    using Windows;

    partial class App {
        public App() {
            var instance = new AppInstance(this);
            if (instance.ShuttingDown == false) {
                new AppConfig(this);
                new AppLogger(this);
            }
        }
    }
}
