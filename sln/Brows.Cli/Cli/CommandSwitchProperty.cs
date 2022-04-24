using System;
using System.Reflection;

namespace Brows.Cli {
    internal class CommandSwitchProperty : CommandTokenProperty {
        new public CommandSwitchAttribute Attribute { get; }

        public char ShortName => Attribute.ShortName;

        public CommandSwitchProperty(PropertyInfo property, CommandSwitchAttribute attribute) : base(property, attribute) {
            Attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        }

        public int Set(CommandToken[] arguments, object obj) {
            var propertyType = Property.PropertyType;
            if (propertyType == typeof(bool)) {
                Property.SetValue(obj, true);
                return 0;
            }
            if (propertyType == typeof(string)) {
                Property.SetValue(obj, arguments[0].Argument);
                return 1;
            }
            if (propertyType.IsValueType) {
                Property.SetValue(obj, arguments[0].ConvertTo(propertyType));
                return 1;
            }
            if (propertyType.IsArray) {
                var elementType = propertyType.GetElementType();
                var array = Array.CreateInstance(elementType, arguments.Length);
                for (var i = 0; i < array.Length; i++) {
                    array.SetValue(arguments[i].ConvertTo(elementType), i);
                }
                Property.SetValue(obj, array);
                return arguments.Length;
            }
            return 0;
        }
    }
}
