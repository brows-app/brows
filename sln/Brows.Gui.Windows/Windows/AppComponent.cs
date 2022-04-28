using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace Brows.Windows {
    public class AppComponent {
        private Type ComponentResourceType =>
            _ComponentResourceType ?? (
            _ComponentResourceType = ComponentResource.GetType());
        private Type _ComponentResourceType;

        private Assembly ComponentResourceAssembly =>
            _ComponentResourceAssembly ?? (
            _ComponentResourceAssembly = ComponentResourceType.Assembly);
        private Assembly _ComponentResourceAssembly;

        private AssemblyName ComponentResourceAssemblyName =>
            _ComponentResourceAssemblyName ?? (
            _ComponentResourceAssemblyName = ComponentResourceAssembly.GetName());
        private AssemblyName _ComponentResourceAssemblyName;

        private static ResourceDictionary SourceResource(string source) {
            try {
                return new ResourceDictionary { Source = new Uri(source) };
            }
            catch {
                return null;
            }
        }

        private static IEnumerable<string> CultureChecks() {
            var culture = Thread.CurrentThread.CurrentUICulture;
            var cultureName = culture.Name;
            yield return cultureName;

            var parentName = culture.Parent?.Name;
            if (parentName != null && parentName != cultureName) {
                yield return parentName;
            }

            var defaultName = "_";
            yield return defaultName;
        }

        internal IComponentResource ComponentResource { get; }

        internal AppComponent(IComponentResource componentResource) {
            ComponentResource = componentResource ?? throw new ArgumentNullException(nameof(componentResource));
        }

        internal IEnumerable<ResourceDictionary> ResourceDictionaries {
            get {
                var assembly = ReferencedAssembly;
                var resource = SourceResource($"pack://application:,,,/{assembly};component/Resource/Resource.xaml");
                if (resource != null) {
                    yield return resource;
                }
                var cultureChecks = CultureChecks();
                foreach (var culture in cultureChecks) {
                    var cultureResource = SourceResource($"pack://application:,,,/{assembly};component/Resource/Culture/{culture}.xaml");
                    if (cultureResource != null) {
                        yield return cultureResource;
                        break;
                    }
                }
            }
        }

        public string ReferencedAssembly =>
            _ReferencedAssembly ?? (
            _ReferencedAssembly = ComponentResourceAssemblyName.Name);
        private string _ReferencedAssembly;

        public static AppComponentDataTemplateSelectorCollection DataTemplateSelector =>
            _DataTemplateSelector ?? (
            _DataTemplateSelector = new AppComponentDataTemplateSelectorCollection());
        private static AppComponentDataTemplateSelectorCollection _DataTemplateSelector;
    }
}
