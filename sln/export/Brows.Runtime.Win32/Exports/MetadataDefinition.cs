using Domore.Runtime.Win32;
using System;

namespace Brows.Exports {
    internal sealed class MetadataDefinition : IMetadataDefinition {
        public PropertyDescription PropertyDescription { get; }

        public MetadataDefinition(PropertyDescription propertyDescription) {
            PropertyDescription = propertyDescription ?? throw new ArgumentNullException(nameof(propertyDescription));
        }

        string IMetadataDefinition.Key =>
            PropertyDescription.CanonicalName;

        string IMetadataDefinition.Name =>
            PropertyDescription.DisplayName;
    }
}
