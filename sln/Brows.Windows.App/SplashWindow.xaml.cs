using Domore.Logs;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace Brows {
    sealed partial class SplashWindow {
        //private void Logging_Event(object sender, LogEventArgs e) {
        //    var list = e?.LogList;
        //    if (list != null) {
        //        void work() {
        //            var text = LogText.Text;
        //            if (text?.Length > 1000) {
        //                text = text.Substring(1000);
        //            }
        //            text = string.Join(Environment.NewLine, list.Prepend(text));
        //            LogText.Text = text;
        //            LogText.ScrollToEnd();
        //        }
        //        if (Dispatcher.Thread != Thread.CurrentThread) {
        //            Dispatcher.BeginInvoke(work);
        //        }
        //        else {
        //            work();
        //        }
        //    }
        //}

        protected sealed override void OnClosing(CancelEventArgs e) {
            //Logging.EventThreshold = LogSeverity.None;
            //Logging.Event -= Logging_Event;
            base.OnClosing(e);
        }

        public SplashWindow() {
            InitializeComponent();
            //Logging.EventThreshold = LogSeverity.Info;
            //Logging.Event += Logging_Event;
        }
    }
}
