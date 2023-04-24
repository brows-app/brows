using Domore.Conf.Cli;
using System;

namespace Brows.Commands {
    public class WorkIOParameter {
        internal PanelPassiveMode Panel() {
            switch (Where) {
                case WorkIOWhere.Auto:
                    return PanelPassiveMode.Auto;
                case WorkIOWhere.Active:
                    return PanelPassiveMode.Active;
                case WorkIOWhere.Next:
                    return PanelPassiveMode.Next;
                case WorkIOWhere.None:
                    return PanelPassiveMode.None;
                case WorkIOWhere.Previous:
                    return PanelPassiveMode.Previous;
                default:
                    throw new InvalidOperationException();
            }
        }

        [CliArgument]
        [CliRequired]
        public WorkIOWhere Where { get; set; }
    }
}
