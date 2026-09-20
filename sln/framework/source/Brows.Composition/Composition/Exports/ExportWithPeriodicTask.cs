using Domore.Logs;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition.Exports;

public abstract class ExportWithPeriodicTask : IExportAndKill {
    private static readonly ILog Log = Logging.For(typeof(ExportWithPeriodicTask));

    private Timer Timer;
    private bool Stop;

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called implicitly")]
    [ImportsReadyCallback]
    private void ImportsReady() {
        if (Log.Info()) {
            Log.Info($"Periodic task starting: {GetType()?.Name}");
        }
        TimerStart();
    }

    private async void TimerCallback(object _) {
        if (Log.Debug()) {
            Log.Debug($"Periodic task triggered: {GetType()?.Name}");
        }
        using (Timer) {
            try {
                var task = PeriodicTask(CancellationToken.None);
                if (task != null) {
                    await task.ConfigureAwait(false);
                }
            }
            catch (Exception ex) {
                Log.Error(ex);
            }
        }
        TimerStart();
    }

    private void TimerStart() {
        if (Stop) {
            return;
        }
        Timer = new(
            TimerCallback,
            state: null,
            dueTime: Period,
            period: TimeSpan.FromMilliseconds(-1));
    }

    protected abstract TimeSpan Period { get; }
    protected abstract Task PeriodicTask(CancellationToken token);

    void IExportAndKill.Kill() {
        if (Log.Info()) {
            Log.Info($"Periodic task stopping: {GetType()?.Name}");
        }
        Stop = true;
    }
}
