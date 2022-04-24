using System.Windows;
using System.Windows.Controls;

namespace Brows {
    partial class CommandContextHintControl {
        public static DataTemplateSelector ItemTemplateSelector { get; } = new ItemTemplateSelectorImpl();

        public CommandContextHintControl() {
            InitializeComponent();
        }

        private class ItemTemplateSelectorImpl : DataTemplateSelector {
            public override DataTemplate SelectTemplate(object item, DependencyObject container) {
                if (item == null) return null;
                var type = item.GetType();
                var template = Application.Current.TryFindResource(new DataTemplateKey(type)) as DataTemplate;
                if (template == null) {
                    template = Application.Current.TryFindResource($"{nameof(CommandContextHint)}_{type.Name}") as DataTemplate;
                }
                return template;
            }
        }
    }
}
