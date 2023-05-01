namespace Brows {
    internal sealed class PanelState {
        public int ItemsCount { get; init; }
        public int ItemsSortingCount { get; init; }
        public int ItemsSelectedCount { get; init; }

        public static PanelState For(Panel panel) {
            return new PanelState {
                ItemsCount = panel?.Entries?.Count ?? 0,
                ItemsSortingCount = panel?.Provider?.Observation?.Sorting?.Count ?? 0,
                ItemsSelectedCount = panel?.Selection?.Count ?? 0
            };
        }
    }
}
