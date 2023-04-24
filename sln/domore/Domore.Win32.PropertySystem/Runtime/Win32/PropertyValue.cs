namespace Domore.Runtime.Win32 {
    public sealed class PropertyValue {
        public PropertyDescription Description { get; }
        public string Display { get; }

        public PropertyValue(PropertyDescription description, string display) {
            Description = description;
            Display = display;
        }
    }
}
