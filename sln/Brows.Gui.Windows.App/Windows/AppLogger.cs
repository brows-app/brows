using Domore.Logs;
using System;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace Brows.Windows {
    internal class AppLogger {
        private static readonly ILog Log = Logging.For(typeof(AppLogger));
        private readonly AppDomain Domain;

        private void Domain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e) {
            if (Log.Debug()) {
                Log.Debug(nameof(Domain.FirstChanceException), e?.Exception);
            }
        }

        private void Domain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            if (Log.Warn()) {
                Log.Warn(nameof(Domain_UnhandledException));
            }
            if (e != null) {
                if (Log.Error()) {
                    Log.Error(e.ExceptionObject);
                    Log.Error($"{nameof(e.IsTerminating)} > {e.IsTerminating}");
                }
            }
        }

        private void Domain_ProcessExit(object sender, EventArgs e) {
            if (Log.Info()) {
                Log.Info(nameof(Domain_ProcessExit));
            }
        }

        private void Domain_DomainUnload(object sender, EventArgs e) {
            if (Log.Info()) {
                Log.Info(nameof(Domain_DomainUnload));
            }
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
            if (Log.Warn()) {
                Log.Warn(nameof(Application_DispatcherUnhandledException));
            }
            if (e != null) {
                if (e.Handled == false) {
                    if (Log.Error()) {
                        Log.Error(e.Exception);
                    }
                }
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e) {
            if (Log.Info()) {
                Log.Info(nameof(Application_Exit));
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e) {
            if (Log.Info()) {
                Log.Info(nameof(Application_Startup));
            }
        }

        public WindowsApplication App { get; }

        public AppLogger(WindowsApplication app) {
            App = app ?? throw new ArgumentNullException(nameof(app));
            App.DispatcherUnhandledException += Application_DispatcherUnhandledException;
            App.Exit += Application_Exit;
            App.Startup += Application_Startup;

            Domain = AppDomain.CurrentDomain;
            Domain.DomainUnload += Domain_DomainUnload;
            Domain.ProcessExit += Domain_ProcessExit;
            Domain.FirstChanceException += Domain_FirstChanceException;
            Domain.UnhandledException += Domain_UnhandledException;
        }
    }
}
