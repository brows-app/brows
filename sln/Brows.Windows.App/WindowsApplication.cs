using System.Windows;

namespace Brows {
    using Windows;

    internal sealed class WindowsApplication : Application {
        public AppExports Exports { get; }
        public AppService Service { get; }
        public IProgramContext Context { get; }

        public WindowsApplication(IProgramContext context) {
            new AppConfig(this);
            new AppLogger(this);
            new AppGlobal(this);
            Context = context;
            Exports = new AppExports(this);
            Service = new AppService(this);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }
    }
}
