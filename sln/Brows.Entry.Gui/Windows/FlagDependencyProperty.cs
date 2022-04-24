using System;
using System.Windows;

namespace Brows.Windows {
    internal class FlagDependencyProperty {
        private DependencyProperty PropertyChanged { get; }

        private FlagDependencyProperty(DependencyProperty propertyChanged) {
            PropertyChanged = propertyChanged ?? throw new ArgumentNullException(nameof(propertyChanged));
        }

        private void PropertyChangedCallback(DependencyObject source, DependencyPropertyChangedEventArgs e) {
            if (null == source) return;
            var type = source.GetType();
            var rKey = $"{type.Name}_{e.NewValue}_{PropertyChanged.Name}";
            var rsrc = Resource.Find(rKey);
            source.SetCurrentValue(PropertyChanged, rsrc);
        }

        public ApplicationResource Resource {
            get => _Resource ?? (_Resource = new ApplicationResource());
            set => _Resource = value;
        }
        private ApplicationResource _Resource;

        public static DependencyProperty Register(Type ownerType, DependencyProperty propertyChanged) {
            var flag = new FlagDependencyProperty(propertyChanged);
            return DependencyProperty.Register(
                name: "Flag",
                propertyType: typeof(string),
                ownerType: ownerType,
                typeMetadata: new PropertyMetadata(
                    defaultValue: "Default",
                    propertyChangedCallback: flag.PropertyChangedCallback));
        }
    }
}
