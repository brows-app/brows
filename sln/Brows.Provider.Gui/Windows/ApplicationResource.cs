using System;
using System.Windows;

namespace Brows.Windows {
    internal class ApplicationResource {
        private static Application Application =>
            _Application ?? (
            _Application = Application.Current);
        private static Application _Application;

        public object Find(object key) {
            return Application.TryFindResource(key);
        }

        public object Find(string flag, Type type, DependencyProperty property) {
            if (null == type) throw new ArgumentNullException(nameof(type));
            if (null == property) throw new ArgumentNullException(nameof(property));
            return Find($"{type.Name}_{flag}_{property.Name}");
        }
    }
}
