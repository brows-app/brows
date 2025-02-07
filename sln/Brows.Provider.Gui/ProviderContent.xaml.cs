using System;
using System.Windows;
using System.Windows.Controls;

namespace Brows {
    partial class ProviderContent {
        public ProviderContent() {
            InitializeComponent();
            DetailContentControl.ContentTemplateSelector = new DetailTemplateSelectorDefault(this);
        }

        private sealed class DetailTemplateSelectorDefault : DataTemplateSelector {
            public ProviderContent Element { get; }

            public DetailTemplateSelectorDefault(ProviderContent element) {
                Element = element ?? throw new ArgumentNullException(nameof(element));
            }

            public sealed override DataTemplate SelectTemplate(object item, DependencyObject container) {
                if (item == null) {
                    return null;
                }
                var key = item.GetType();
                var resource = Element.FindResource(key);
                var template = resource as DataTemplate;
                return template;
            }
        }
    }
}
