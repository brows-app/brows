using Domore.Runtime.InteropServices;
using Domore.Runtime.InteropServices.ComTypes;
using System;

namespace Domore.Runtime.Win32 {
    public sealed class PropertyDescription {
        private PropertyDescription(string canonicalName, string displayName, PROPERTYKEY key) {
            CanonicalName = canonicalName;
            DisplayName = displayName;
            Key = key;
        }

        internal static PropertyDescription Of(IPropertyDescription item) {
            var key = item.GetPropertyKey();
            if (key.fmtid == Guid.Empty) {
                return null;
            }
            var canonicalName = item.GetCanonicalName();
            if (canonicalName == null) {
                return null;
            }
            var displayName = item.GetDisplayName();
            return new PropertyDescription(
                canonicalName: canonicalName,
                displayName: displayName,
                key: key);
        }

        public string CanonicalName { get; }
        public string DisplayName { get; }
        public PROPERTYKEY Key { get; }
    }
}
