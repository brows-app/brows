using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;

namespace Brows.ComponentModel.Composition.Hosting {
    public class Composer {
        public Composition Compose() {
            using (var catalog = new AggregateCatalog()) {
                catalog.Catalogs.Add(new ApplicationCatalog());
                using (var container = new CompositionContainer(catalog)) {
                    var composition = new Composition();
                    container.ComposeParts(
                        composition.Command,
                        composition.ComponentResource,
                        composition.EntryProviderFactory);
                    return composition;
                }
            }
        }
    }
}
