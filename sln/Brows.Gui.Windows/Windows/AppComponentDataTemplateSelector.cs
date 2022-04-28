using System.Windows;
using System.Windows.Controls;

namespace Brows.Windows {
    internal class AppComponentDataTemplateSelector : DataTemplateSelector {
        private Application Application =>
            _Application ?? (
            _Application = Application.Current);
        private Application _Application;

        public string Kind { get; }

        public AppComponentDataTemplateSelector(string kind) {
            Kind = kind;
        }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            if (item == null) return null;

            var kind = Kind;
            var type = item.GetType();
            var typeName = type.Name;
            var typeNameKey = string.IsNullOrWhiteSpace(kind)
                ? typeName
                : $"{kind}_{typeName}";

            var resource = Application.TryFindResource(typeNameKey);
            if (resource == null) {
                resource = Application.TryFindResource(new DataTemplateKey(type));
            }

            var dataTemplate = resource as DataTemplate;
            return dataTemplate == null
                ? base.SelectTemplate(item, container)
                : dataTemplate;
        }
    }
}
