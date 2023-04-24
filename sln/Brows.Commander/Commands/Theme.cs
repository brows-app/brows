using Brows.Config;
using Domore.Conf;
using Domore.Conf.Cli;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class Theme : Command<Theme.Parameter> {
        private IConfig<CommanderTheme> Data =>
            _Data ?? (
            _Data = Configure.Data<CommanderTheme>());
        private IConfig<CommanderTheme> _Data;

        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasCommander(out var commander) == false) return false;
            if (context.HasParameter(out var parameter) == false) return false;
            return context.Operate(async (progress, token) => {
                var theme = await Data.Load(token);
                theme.Base = parameter.Base;
                theme.Background = parameter.Background;
                theme.Foreground = parameter.Foreground;
                CommanderTheme.Apply(theme);
                return true;
            });
        }

        protected sealed override async Task Init(CancellationToken token) {
            var theme = await Data.Load(token);
            CommanderTheme.Apply(theme);
        }

        public sealed class Parameter {
            [CliArgument]
            [CliRequired]
            public string Base { get; set; }

            [Conf("bg")]
            public string Background { get; set; }

            [Conf("fg")]
            public string Foreground { get; set; }
        }
    }
}
