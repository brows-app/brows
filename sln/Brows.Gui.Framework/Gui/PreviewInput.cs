namespace Brows.Gui {
    public class PreviewInput : IPreviewInput {
        public string EntryID { get; }
        public string EntryFile { get; }

        public string ID =>
            _ID ?? (
            _ID = EntryID?.Trim() ?? "");
        private string _ID;

        public string File =>
            _File ?? (
            _File = EntryFile?.Trim() ?? "");
        private string _File;

        public PreviewInput(string entryID, string entryFile) {
            EntryID = entryID;
            EntryFile = entryFile;
        }

        public override string ToString() {
            return ID;
        }

        public override bool Equals(object obj) {
            return
                obj is PreviewInput other &&
                other.ID.Equals(ID) &&
                other.File.Equals(File);
        }

        public override int GetHashCode() {
            unchecked {
                var
                hashCode = (int)2166136261;
                hashCode = (hashCode * 16777619) ^ ID.GetHashCode();
                hashCode = (hashCode * 16777619) ^ File.GetHashCode();
                return hashCode;
            }
        }
    }
}
