using System.Linq;

namespace Brows.Commands {
    using DataKey;

    internal sealed class Hide : DataKeyCommand<Hide.Parameter> {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (context.HasParameter(out var parameter) == false) {
                return false;
            }
            if (active.HasView(out var view) == false) {
                return false;
            }
            var workable = Workable(context);
            if (workable == false) {
                return false;
            }
            var removed = view.Remove(parameter.Args.ToArray());
            if (removed[0] != null) {
                return true;
            }
            return false;
        }

        public sealed class Parameter : DataKeyCommandParameter {
        }
    }
}
