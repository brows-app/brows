using Brows.Commands.DataKey;
using System.Linq;

namespace Brows.Commands {
    internal sealed class Fit : DataKeyCommand<Fit.Parameter> {
        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.GetParameter(out var parameter)) return false;
            if (false == active.HasView(out var view)) return false;
            if (false == active.HasProvider(out IProvider provider)) return false;
            var valid = Validate(provider.Data.Key, parameter.Args);
            if (valid == false) {
                return false;
            }
            var args = parameter.Args.ToArray();
            var fit = view.Fit(args);
            if (fit.Any(f => f != null)) {
                return true;
            }
            return false;
        }

        public sealed class Parameter : DataKeyCommandParameter {
        }
    }
}
