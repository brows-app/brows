namespace Brows {
    public class EntryColumn : IEntryColumn {
        public double Width { get; init; } = double.NaN;
        public IComponentResourceKey Resolver { get; init; }
    }
}
