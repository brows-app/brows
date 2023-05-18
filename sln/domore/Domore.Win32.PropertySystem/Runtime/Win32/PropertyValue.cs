namespace Domore.Runtime.Win32 {
    public sealed class PropertyValue {
        public PropertyDescription Description { get; }
        public string Display { get; }
        public object Object { get; }

        public PropertyValue(PropertyDescription description, string display, object @object) {
            Description = description;
            Display = display;
            Object = @object;
        }
    }
}
