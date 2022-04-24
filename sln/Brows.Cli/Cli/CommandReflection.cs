using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Brows.Cli {
    internal class CommandReflection {
        private PropertyInfo[] PublicInstanceProperties =>
            _PublicInstanceProperties ?? (
            _PublicInstanceProperties = Type.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        private PropertyInfo[] _PublicInstanceProperties;

        private CommandTokenProperty[] CommandTokenProperties =>
            _CommandTokenProperties ?? (
            _CommandTokenProperties = PublicInstanceProperties
                .Select(property => new {
                    Property = property,
                    Attribute = property.GetCustomAttribute<CommandTokenAttribute>(inherit: true)
                })
                .Where(item => item.Attribute != null)
                .Select(item => CommandTokenProperty.Create(item.Property, item.Attribute))
                .ToArray());
        private CommandTokenProperty[] _CommandTokenProperties;

        private IEnumerable<CommandSwitchProperty> CommandSwitchProperties =>
            CommandTokenProperties.OfType<CommandSwitchProperty>();

        private IEnumerable<CommandArgumentProperty> CommandArgumentProperties =>
            CommandTokenProperties.OfType<CommandArgumentProperty>();

        public Type Type { get; }

        public CommandReflection(Type type) {
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }

        public CommandSwitchProperty SwitchProperty(string @switch) {
            return CommandSwitchProperties.FirstOrDefault(property => property.Name == @switch);
        }

        public CommandSwitchProperty SwitchProperty(char @switch) {
            return CommandSwitchProperties.FirstOrDefault(property => property.ShortName == @switch);
        }

        public CommandSwitchProperty[] SwitchProperties(char[] switches) {
            if (null == switches) throw new ArgumentNullException(nameof(switches));
            return CommandSwitchProperties.Where(property => switches.Contains(property.ShortName)).ToArray();
        }

        public CommandSwitchProperty[] SwitchProperties() {
            return CommandSwitchProperties.ToArray();
        }

        public CommandArgumentProperty[] ArgumentProperties() {
            return CommandArgumentProperties.ToArray();
        }
    }
}
