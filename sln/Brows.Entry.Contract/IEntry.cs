namespace Brows {
    using Gui;

    public interface IEntry {
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
        void End();
        void Open();
        void Notify(bool state);
        void Refresh(EntryRefresh flags);
        void Refresh(params string[] keys);
        void Rename(string name);
        string Rename();
    }
}
