using Domore.Notification;

namespace Brows.Gui {
    public abstract class Image : Notifier {
        public abstract object Source { get; }
        public abstract ImageSize Size { get; set; }
    }
}
