using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Brows;

/// <summary>
/// WPF dispatcher tools for tests.
/// </summary>
public static class DispatcherTools {
    private static readonly int PumpCount = 5;
    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Runs <paramref name="body"/> on a dedicated STA thread that owns a WPF
    /// <see cref="Dispatcher"/> whose <see cref="DispatcherSynchronizationContext"/>
    /// is installed, so async continuations post back to the dispatcher and
    /// <see cref="Pump"/> can drive them deterministically.
    /// The worker is a background thread; a timeout requests dispatcher shutdown.
    /// </summary>
    /// <param name="body">
    /// The work to run. It receives the thread's <see cref="Dispatcher"/>.
    /// </param>
    /// <exception cref="TimeoutException">
    /// <paramref name="body"/> did not finish in time.
    /// </exception>
    public static void Run(Action<Dispatcher> body) {
        ArgumentNullException.ThrowIfNull(body);
        var done = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher dispatcher = null;
        var thread = new Thread(() => {
            Exception error = null;
            try {
                var current = Dispatcher.CurrentDispatcher;
                Volatile.Write(ref dispatcher, current);
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(current));
                body(current);
            }
            catch (Exception e) {
                error = e;
            }
            finally {
                try {
                    dispatcher?.InvokeShutdown();
                }
                catch (Exception e) {
                    error ??= e;
                }
                done.SetResult(error);
            }
        }) { IsBackground = true };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (done.Task.Wait(RunTimeout) == false) {
            var current = Volatile.Read(ref dispatcher);
            if (current is not null) {
                try {
                    current.BeginInvokeShutdown(DispatcherPriority.Send);
                }
                catch (InvalidOperationException) {
                    /*
                     * The dispatcher may have finished shutting down as the wait expired.
                     */
                }
            }
            throw new TimeoutException($"{nameof(Run)} > {RunTimeout}");
        }
        var error = done.Task.GetAwaiter().GetResult();
        if (error is not null) {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    /// <summary>
    /// Drains the operations the owning thread has queued up to a bound, so
    /// await continuations run to completion at the call site.
    /// </summary>
    /// <param name="dispatcher">
    /// The <see cref="Dispatcher"/> to drain. It must be owned by the calling
    /// thread.
    /// </param>
    public static void Pump(Dispatcher dispatcher) {
        ArgumentNullException.ThrowIfNull(dispatcher);
        for (var i = 0; i < PumpCount; i++) {
            var frame = new DispatcherFrame();
            _ = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }
}
