using System;
using System.Reflection;

namespace Brows.Cli {
    internal class CommandTokenProperty {
        private string Map(Type type) {
            if (null == type) throw new ArgumentNullException(nameof(type));
            if (type == typeof(int)) return "int";
            if (type == typeof(string)) return "string";

            return type.Name;
        }

        protected CommandTokenProperty(PropertyInfo property, CommandTokenAttribute attribute) {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            Attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        }

        public PropertyInfo Property { get; }
        public CommandTokenAttribute Attribute { get; }

        public string Name => Attribute.Name ?? Property.Name;
        public Type Type => Property.PropertyType;
        public bool Required => Attribute.Required;
        public string DefaultValue => Attribute.DefaultValue;
        public string HelpToken {
            get {
                var s = "";
                var sw = Attribute as CommandSwitchAttribute;
                if (!Required) s += "[";
                if (sw != null) s += "--";

                s += Name;
                if (sw != null && sw.ShortName != 0) {
                    s += $",-{sw.ShortName}";
                }

                var type = true;
                if (sw != null && Type == typeof(bool)) type = false;
                if (sw == null && Type == typeof(string)) type = false;
                if (type) {
                    if (sw != null) s += " ";
                    s += "<";
                    s += Map(Type);
                    s += ">";
                }

                if (!Required) s += "]";
                return s;
            }
        }

        public static CommandTokenProperty Create(PropertyInfo property, CommandTokenAttribute attribute) {
            if (attribute is CommandArgumentAttribute caa) return new CommandArgumentProperty(property, caa);
            if (attribute is CommandSwitchAttribute csa) return new CommandSwitchProperty(property, csa);
            throw new ArgumentException(paramName: nameof(attribute), message: $"{nameof(attribute)} [{attribute}]");
        }
    }
}
