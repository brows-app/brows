namespace Brows.Gui {
    public struct ImageSize {
        public int Width { get; }
        public int Height { get; }

        public ImageSize(int width, int height) {
            Width = width;
            Height = height;
        }

        public override string ToString() {
            return $"{Width}x{Height}";
        }

        public override bool Equals(object obj) {
            return
                obj is ImageSize other &&
                other.Width.Equals(Width) &&
                other.Height.Equals(Height);
        }

        public override int GetHashCode() {
            unchecked {
                var
                hashCode = (int)2166136261;
                hashCode = (hashCode * 16777619) ^ Width.GetHashCode();
                hashCode = (hashCode * 16777619) ^ Height.GetHashCode();
                return hashCode;
            }
        }
    }
}
