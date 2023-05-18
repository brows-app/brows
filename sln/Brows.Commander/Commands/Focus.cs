using Domore.Conf.Cli;
using System.Reflection;

namespace Brows.Commands {
    internal sealed class Focus : Command<Focus.Parameter> {
        protected override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.GetParameter(out var parameter)) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == active.HasProvider(out IProvider provider)) return false;
            var property = provider.GetType().GetProperty(parameter.Where, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null) return false;
            if (property.CanRead == false) return false;
            var obj = property.GetValue(provider);
            if (obj == null) {
                return false;
            }
            var focus = obj as IProviderFocus;
            if (focus == null) {
                return false;
            }
            var focused = focus.Set();
            if (focused == false) {
                return false;
            }
            return true;
        }

        public sealed class Parameter {
            [CliArgument]
            public string Where { get; set; }
        }
    }
}
