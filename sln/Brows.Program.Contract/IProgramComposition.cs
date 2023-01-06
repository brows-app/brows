using System.Collections.Generic;

namespace Brows {
    public interface IProgramComposition {
        public IReadOnlyList<ICommand> Commands { get; }
        public IReadOnlyList<IComponentResource> ComponentResources { get; }
        public IReadOnlyList<IEntryProviderFactory> EntryProviderFactories { get; }
    }
}
