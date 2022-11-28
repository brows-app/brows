namespace Brows.Gui {
    public class OverlayInput : IOverlayInput {
        public string EntryID { get; }

        public string ID =>
            _ID ?? (
            _ID = (EntryID ?? "").Trim());
        private string _ID;

        public OverlayInput(string entryID) {
            EntryID = entryID;
        }

        public override string ToString() {
            return ID;
        }

        public override bool Equals(object obj) {
            return
                obj is OverlayInput other &&
                other.ID.Equals(ID);
        }

        public override int GetHashCode() {
            unchecked {
                var
                hashCode = (int)2166136261;
                hashCode = (hashCode * 16777619) ^ ID.GetHashCode();
                return hashCode;
            }
        }
    }
}
