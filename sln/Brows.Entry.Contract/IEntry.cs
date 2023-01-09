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
        IPreviewText PreviewText { get; }

        void Begin(IEntryBrowser browser);
        void Begin(IEntryView view);
        void End();
        void Open();
        void Edit();
        void Notify(bool state);
        void Refresh(EntryRefresh flags);
        void Rename(string name);
        string Rename();
    }
}
