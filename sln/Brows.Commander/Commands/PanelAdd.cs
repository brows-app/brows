using Brows.Config;
using Domore.Conf.Cli;
using System.Linq;

namespace Brows.Commands {
    internal sealed class PanelAdd : Command<PanelAdd.Parameter> {
        private IConfig<CommanderConfig> CommanderConfig =>
            _CommanderConfig ?? (
            _CommanderConfig = Configure.File<CommanderConfig>());
        private IConfig<CommanderConfig> _CommanderConfig;

        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.GetParameter(out var parameter)) return false;
            if (false == context.HasCommander(out var commander)) return false;
            var id = parameter.ID?.Trim() ?? "";
            if (id == "") {
                switch (parameter.From) {
                    case IDGenerator.CurrentEntry: {
                        if (context.HasPanel(out var active) && active.HasEntry(out var current)) {
                            id = current.ID;
                        }
                        break;
                    }
                    case IDGenerator.ActivePanel: {
                        if (context.HasPanel(out var active) && active.HasProvider(out IProvider provider)) {
                            id = provider.ID;
                        }
                        break;
                    }
                }
            }
            return context.Operate(async (progress, token) => {
                if (string.IsNullOrWhiteSpace(id)) {
                    var config = await CommanderConfig.Load(token);
                    var loaded = config.LoadFirst.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
                    if (loaded != null) {
                        return await commander.AddPanel(loaded, token);
                    }
                }
                else {
                    return await commander.AddPanel(id, token);
                }
                return false;
            });
        }

        public enum IDGenerator {
            ActivePanel,
            CurrentEntry
        }

        public sealed class Parameter {
            [CliArgument]
            public string ID { get; set; }

            [CliDisplay(false)]
            public IDGenerator? From { get; set; }
        }
    }
}
