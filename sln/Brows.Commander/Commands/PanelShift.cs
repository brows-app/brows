using Domore.Conf.Cli;

namespace Brows.Commands {
    internal sealed class PanelShift : Command<PanelShift.Parameter> {
        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.HasCommander(out var commander)) return false;
            if (false == context.HasParameter(out var parameter)) return false;
            var column = active.Column;
            switch (parameter.Direction) {
                case Direction.Left:
                    column--;
                    break;
                case Direction.Right:
                    column++;
                    break;
            }
            return context.Operate(async (progress, token) => {
                return await commander.ShiftPanel(active, column, token);
            });
        }

        public enum Direction {
            Left,
            Right
        }

        public sealed class Parameter {
            [CliArgument]
            public Direction Direction { get; set; }
        }
    }
}
