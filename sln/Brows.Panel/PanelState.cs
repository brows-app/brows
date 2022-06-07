namespace Brows {
    internal class PanelState {
        public int ItemsCount { get; init; }
        public int ItemsSelectedCount { get; init; }

        public static PanelState For(Panel panel) {
            return new PanelState {
                ItemsCount = panel.Entries.Count,
                ItemsSelectedCount = panel.EntriesSelected.Count
            };
        }
    }
}
