using Brows.Windows.Controls;
using System;
using System.Threading.Tasks;

namespace Brows.Terminal;

internal sealed class TerminalEmulatorSessionAdapter : ITerminalSessionAdapter {
    private readonly TerminalEmulator Terminal;

    internal TerminalEmulatorSessionAdapter(TerminalEmulator terminal) {
        Terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
    }

    public bool IsRunning {
        get {
            Terminal.Dispatcher.VerifyAccess();
            return Terminal.IsRunning;
        }
    }

    public Task StartAsync() {
        Terminal.Dispatcher.VerifyAccess();
        return Terminal.StartAsync();
    }

    public Task StopAsync() {
        Terminal.Dispatcher.VerifyAccess();
        return Terminal.StopAsync();
    }
}
