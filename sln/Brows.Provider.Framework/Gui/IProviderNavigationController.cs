namespace Brows.Gui {
    public interface IProviderNavigationController {
        bool Focused { get; }
        bool Focus();
        bool Current(ProviderNavigationNode node);
        ProviderNavigationNode Current();
    }
}
