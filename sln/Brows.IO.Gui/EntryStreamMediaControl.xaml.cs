using Domore.Logs;
using System;
using System.Windows;

namespace Brows {
    partial class EntryStreamMediaControl {
        private static readonly ILog Log = Logging.For(typeof(EntryStreamMediaControl));

        private void ChangeEntryStreamGui() {
            EntryStreamGui?.State?.Media?.Change(loading: true);
            MediaElement.Source = null;
            var err = default(Exception);
            try {
                MediaElement.Source = new Uri(EntryStreamGui?.Source?.SourceFile);
            }
            catch (Exception ex) {
                if (Log.Debug()) {
                    Log.Debug(ex);
                }
                err = ex;
            }
            EntryStreamGui?.State?.Media?.Change(loading: false, success: err == null);
        }

        protected sealed override string EntryStreamViewName => "Media";

        protected sealed override void OnEntryStreamGuiChanged(DependencyPropertyChangedEventArgs e) {
            ChangeEntryStreamGui();
            base.OnEntryStreamGuiChanged(e);
        }

        public EntryStreamMediaControl() {
            InitializeComponent();
        }
    }
}
