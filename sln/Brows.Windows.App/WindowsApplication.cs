using Brows.Windows;
using System.Threading;
using System.Windows;

namespace Brows {
    internal sealed class WindowsApplication : Application {
        public AppExports Exports { get; }
        public AppService Service { get; }
        public CancellationToken Token { get; }

        public WindowsApplication(CancellationToken token) {
            _ = new AppLogger(this);
            Token = token;
            Exports = new AppExports(this);
            Service = new AppService(this);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }
    }
}
