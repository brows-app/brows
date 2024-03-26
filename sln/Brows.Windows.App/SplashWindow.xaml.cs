using Domore.Logs;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace Brows {
    sealed partial class SplashWindow {
        private void Logging_LogEvent(object sender, LogEventArgs e) {
            var list = e?.LogList;
            if (list != null) {
                void work() {
                    //LogText.Text = string.Join(Environment.NewLine, list.Prepend(LogText.Text));
                    //LogText.ScrollToEnd();
                }
                if (Dispatcher.Thread != Thread.CurrentThread) {
                    Dispatcher.BeginInvoke(work);
                }
                else {
                    work();
                }
            }
        }

        protected sealed override void OnClosing(CancelEventArgs e) {
            //Logging.LogEventSeverity = LogSeverity.None;
            //Logging.LogEvent -= Logging_LogEvent;
            base.OnClosing(e);
        }

        public SplashWindow() {
            InitializeComponent();
            //Logging.LogEventSeverity = LogSeverity.Info;
            //Logging.LogEvent += Logging_LogEvent;
        }
    }
}
