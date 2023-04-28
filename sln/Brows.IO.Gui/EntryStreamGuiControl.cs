using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Brows {
    public abstract class EntryStreamGuiControl : UserControl {
        private void This_Loaded(object sender, RoutedEventArgs e) {
            ChangeEntryStreamGui(null, EntryStreamGui);
        }

        private void This_Unloaded(object sender, RoutedEventArgs e) {
            ChangeEntryStreamGui(EntryStreamGui, null);
        }

        private void EntryStreamGui_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (sender == EntryStreamGui) {
                if (e != null) {
                    if (e.PropertyName == nameof(EntryStreamGui.View)) {
                        EntryStreamView = EntryStreamGui?.View;
                    }
                }
            }
        }

        private void ChangeEntryStreamGui(IEntryStreamGui oldValue, IEntryStreamGui newValue) {
            if (oldValue != null) {
                PropertyChangedEventManager.RemoveHandler(oldValue, EntryStreamGui_PropertyChanged, nameof(EntryStreamGui.View));
            }
            if (newValue != null) {
                PropertyChangedEventManager.AddHandler(newValue, EntryStreamGui_PropertyChanged, nameof(EntryStreamGui.View));
            }
            EntryStreamView = newValue?.View;
        }

        private static void OnEntryStreamViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as EntryStreamGuiControl;
            if (c != null) {
                c.OnEntryStreamViewChanged(e);
            }
        }

        private static void OnEntryStreamGuiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var c = d as EntryStreamGuiControl;
            if (c != null) {
                c.OnEntryStreamGuiChanged(e);
            }
        }

        protected abstract string EntryStreamViewName { get; }

        protected virtual void OnEntryStreamViewChanged(DependencyPropertyChangedEventArgs e) {
            var view = e.NewValue as string;
            if (view == EntryStreamViewName) {
                Visibility = Visibility.Visible;
            }
            else {
                Visibility = Visibility.Hidden;
            }
        }

        protected virtual void OnEntryStreamGuiChanged(DependencyPropertyChangedEventArgs e) {
            ChangeEntryStreamGui(e.OldValue as IEntryStreamGui, e.NewValue as IEntryStreamGui);
        }

        public static readonly DependencyProperty EntryStreamViewProperty = DependencyProperty.Register(
            name: nameof(EntryStreamView),
            propertyType: typeof(string),
            ownerType: typeof(EntryStreamGuiControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: null,
                propertyChangedCallback: OnEntryStreamViewChanged));

        public static readonly DependencyProperty EntryStreamGuiProperty = DependencyProperty.Register(
            name: nameof(EntryStreamGui),
            propertyType: typeof(IEntryStreamGui),
            ownerType: typeof(EntryStreamGuiControl),
            typeMetadata: new PropertyMetadata(
                defaultValue: null,
                propertyChangedCallback: OnEntryStreamGuiChanged));

        public string EntryStreamView {
            get => GetValue(EntryStreamViewProperty) as string;
            set => SetValue(EntryStreamViewProperty, value);
        }

        public IEntryStreamGui EntryStreamGui {
            get => GetValue(EntryStreamGuiProperty) as IEntryStreamGui;
            set => SetValue(EntryStreamGuiProperty, value);
        }

        public EntryStreamGuiControl() {
            Loaded += This_Loaded;
            Unloaded += This_Unloaded;
            Visibility = Visibility.Hidden;
        }
    }
}
