using Domore.Notification;
using System.ComponentModel;

namespace Brows.Diagnostics {
    internal class ProcessStreamItem : Notifier {
        private static readonly PropertyChangedEventArgs LineEvent = new(nameof(Line));

        public string Line { get; private set; }

        public void Append(char c) {
            Line += c;
            NotifyPropertyChanged(LineEvent);
        }
    }
}
