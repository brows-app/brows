using System.Windows;
using System.Windows.Controls;

namespace Brows {
    public abstract class EntryStreamGuiControl : UserControl {
        private static void OnEntryStreamGuiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as EntryStreamGuiControl;
            if (c != null) {
                c.OnEntryStreamGuiChanged(e);
            }
        }

        protected virtual void OnEntryStreamGuiChanged(DependencyPropertyChangedEventArgs e) {
        }

        public static readonly DependencyProperty EntryStreamGuiProperty = DependencyProperty.Register(
            name: nameof(EntryStreamGui),
            propertyType: typeof(IEntryStreamGui),
            ownerType: typeof(EntryStreamGuiControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: null,
                propertyChangedCallback: OnEntryStreamGuiChanged));

        public IEntryStreamGui EntryStreamGui {
            get => GetValue(EntryStreamGuiProperty) as IEntryStreamGui;
            set => SetValue(EntryStreamGuiProperty, value);
        }
    }
}
