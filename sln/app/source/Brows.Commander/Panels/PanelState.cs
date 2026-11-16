using Brows.Providers;

namespace Brows.Panels;

internal sealed class PanelState {
    public bool ItemsObserved { get; init; }
    public int ItemsCount { get; init; }
    public int ItemsSortingCount { get; init; }
    public int ItemsSelectedCount { get; init; }

    public static PanelState For(Panel panel) {
        var entryProvider = panel?.Provider as IEntryProvider;
        return new PanelState {
            ItemsObserved = entryProvider is not null,
            ItemsCount = panel?.Entries?.Count ?? 0,
            ItemsSortingCount = entryProvider?.Observation?.Sorting?.Count ?? 0,
            ItemsSelectedCount = panel?.Selection?.Count ?? 0
        };
    }
}
