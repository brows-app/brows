namespace Brows {
    using Gui;

    public interface IEntry {
        event EntryRefreshedEventHandler Refreshed;

        IEntryData this[string key] { get; }

        string ID { get; }
        string Name { get; }
        string File { get; }
        bool Selected { get; set; }

        Image Icon { get; }
        Image Thumbnail { get; }
        Image PreviewImage { get; }

        void Begin(IEntryBrowser browser);
        void Begin(IEntryView view);
        void End();
        void Open();
        void Edit();
        void Detail();
        void Refresh(EntryRefresh flags);
        void Rename(string name);
        string Rename();
        T State<T>();
        T State<T>(T value);
    }
}
