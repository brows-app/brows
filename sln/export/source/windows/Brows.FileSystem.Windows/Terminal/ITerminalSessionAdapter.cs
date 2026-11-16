using System.Threading.Tasks;

namespace Brows.Terminal;

internal interface ITerminalSessionAdapter {
    bool IsRunning { get; }

    Task StartAsync();

    Task StopAsync();
}
