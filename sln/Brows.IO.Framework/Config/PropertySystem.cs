using Domore.Notification;
using System.Collections.Generic;

namespace Brows.Config {
    internal class PropertySystem : Notifier {
        public IReadOnlyList<PropertyItem> Properties {
            get => _Properties ?? (_Properties = new List<PropertyItem>());
            set => Change(ref _Properties, value, nameof(Properties));
        }
        private IReadOnlyList<PropertyItem> _Properties;
    }
}
