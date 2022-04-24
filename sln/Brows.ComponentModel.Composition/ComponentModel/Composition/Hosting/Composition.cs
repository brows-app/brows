namespace Brows.ComponentModel.Composition.Hosting {
    public class Composition {
        public CommandPart Command { get; } = new CommandPart();
        public ComponentResourcePart ComponentResource { get; } = new ComponentResourcePart();
        public EntryProviderFactoryPart EntryProviderFactory { get; } = new EntryProviderFactoryPart();
    }
}
