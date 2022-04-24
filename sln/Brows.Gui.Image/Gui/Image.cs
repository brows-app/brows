namespace Brows.Gui {
    using ComponentModel;

    public abstract class Image : NotifyPropertyChanged {
        public abstract object Source { get; }
        public abstract ImageSize Size { get; set; }
    }
}
