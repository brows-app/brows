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
            var type = item.GetType();
            var name = type == typeof(string) ? $"{item}" : type.Name;
            var kind = Kind;
            var nameKey = string.IsNullOrWhiteSpace(kind)
                ? name
                : $"{kind}_{name}";
            var resource = Application.TryFindResource(nameKey);
            var dataTemplate = resource as DataTemplate;
            return dataTemplate == null
                ? base.SelectTemplate(item, container)
                : dataTemplate;
        }
    }
}
