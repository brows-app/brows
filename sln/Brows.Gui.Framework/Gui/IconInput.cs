namespace Brows.Gui {
    public class IconInput : IIconInput {
        public string EntryID { get; }
        public IconStock Stock { get; }

        public string ID =>
            _ID ?? (
            _ID = (EntryID ?? "").Trim());
        private string _ID;

        public IconInput(IconStock stock, string entryID) {
            Stock = stock;
            EntryID = entryID;
        }

        public IconInput(IconStock stock) : this(stock, null) {
        }

        public override string ToString() {
            return ID == ""
                ? $"{Stock}"
                : $"{Stock}: {ID}";
        }

        public override bool Equals(object obj) {
            return
                obj is IconInput other &&
                other.ID.Equals(ID) &&
                other.Stock.Equals(Stock);
        }

        public override int GetHashCode() {
            unchecked {
                var
                hashCode = (int)2166136261;
                hashCode = (hashCode * 16777619) ^ ID.GetHashCode();
                hashCode = (hashCode * 16777619) ^ Stock.GetHashCode();
                return hashCode;
            }
        }
    }
}
