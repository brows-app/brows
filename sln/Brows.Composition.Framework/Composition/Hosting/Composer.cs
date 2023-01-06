using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Linq;

namespace Brows.Composition.Hosting {
    public class Composer {
        private ComposablePartCatalog Catalog() {
            return new AggregateCatalog(catalogs: new ComposablePartCatalog[] {
                new ApplicationCatalog()
            });
        }

        public IReadOnlyList<IProgram> Programs() {
            var part = new ProgramPart();
            using (var catalog = Catalog()) {
                using (var container = new CompositionContainer(catalog)) {
                    container.ComposeParts(part);
                }
            }
            return part.Collection.ToList();
        }

        public IProgramComposition ProgramComposition() {
            var command = new CommandPart();
            var componentResource = new ComponentResourcePart();
            var entryProviderFactory = new EntryProviderFactoryPart();
            using (var catalog = Catalog()) {
                using (var container = new CompositionContainer(catalog)) {
                    container.ComposeParts(
                        command,
                        componentResource,
                        entryProviderFactory);
                }
            }
            return new ProgramComposition(
                command.Collection.ToList(),
                componentResource.Collection.ToList(),
                entryProviderFactory.Collection.ToList());
        }
    }
}
