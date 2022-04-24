using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Brows.Cli {
    internal class CommandArgumentProperty : CommandTokenProperty {
        public bool Aggregate => Attribute.Aggregate;

        public new CommandArgumentAttribute Attribute { get; }

        public CommandArgumentProperty(PropertyInfo property, CommandArgumentAttribute attribute) : base(property, attribute) {
            Attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        }

        public int Set(IReadOnlyList<CommandToken> tokens, int order, object obj) {
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            if (tokens.Count == 0) {
                return 0;
            }
            var attributeOrder = Attribute.Order;
            if (attributeOrder >= 0 && attributeOrder != order) {
                return 0;
            }
            var aggregate = Attribute.Aggregate;
            if (aggregate) {
                Property.SetValue(obj, string.Join(" ", tokens.Select(t => t.Argument)));
                return tokens.Count;
            }
            var token = tokens[0];
            var propertyType = Property.PropertyType;
            if (propertyType == typeof(string)) {
                Property.SetValue(obj, token.Argument);
            }
            else {
                Property.SetValue(obj, token.ConvertTo(propertyType));
            }
            return 1;
        }
    }
}
