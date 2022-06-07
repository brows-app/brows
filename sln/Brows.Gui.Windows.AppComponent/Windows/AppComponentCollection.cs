using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Brows.Windows {
    using Translation;

    public class AppComponentCollection : IEnumerable<AppComponent> {
        private readonly List<AppComponent> List;

        private AppComponentCollection(IEnumerable<AppComponent> collection) {
            List = new List<AppComponent>(collection);
        }

        public static AppComponentCollection From(IEnumerable<IComponentResource> componentResources) {
            return new AppComponentCollection(componentResources.Select(item => new AppComponent(item)));
        }

        public IEnumerable<ResourceDictionary> ResourceDictionaries {
            get {
                foreach (var component in List) {
                    foreach (var dictionary in component.ResourceDictionaries) {
                        yield return dictionary;
                    }
                }
            }
        }

        public ITranslation Translation() {
            return new AppComponentTranslation(this);
        }

        public IEnumerator<AppComponent> GetEnumerator() {
            return ((IEnumerable<AppComponent>)List).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)List).GetEnumerator();
        }
    }
}
