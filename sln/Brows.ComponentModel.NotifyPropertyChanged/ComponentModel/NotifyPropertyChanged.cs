using Domore.Notification;
using System.ComponentModel;

namespace Brows.ComponentModel {
    using Globalization;

    public class NotifyPropertyChanged : Notifier {
        protected bool NotifyState { get; set; } = true;

        protected override void OnPropertyChanged(PropertyChangedEventArgs e) {
            if (NotifyState) {
                base.OnPropertyChanged(e);
            }
        }

        public ITranslation Translate =>
            Global.Translation;
    }
}
