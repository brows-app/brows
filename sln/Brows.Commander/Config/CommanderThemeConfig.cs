using Domore.Notification;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    internal class CommanderThemeConfig {
        private IConfig<Theme> Config =>
            _Config ?? (
            _Config = Configure.Data<Theme>());
        private IConfig<Theme> _Config;

        private class Theme : Notifier {
            public string Base {
                get => _Base;
                set => Change(ref _Base, value, nameof(Base));
            }
            private string _Base;

            public string Background {
                get => _Background;
                set => Change(ref _Background, value, nameof(Background));
            }
            private string _Background;

            public string Foreground {
                get => _Foreground;
                set => Change(ref _Foreground, value, nameof(Foreground));
            }
            private string _Foreground;
        }

        public async Task<CommanderTheme> Load(CancellationToken cancellationToken) {
            var config = await Config.Load(cancellationToken);
            return new CommanderTheme(config.Base, config.Background, config.Foreground);
        }

        public async Task Save(CommanderTheme theme, CancellationToken cancellationToken) {
            var config = await Config.Load(cancellationToken);
            config.Background = theme?.Background;
            config.Base = theme?.Base;
            config.Foreground = theme?.Foreground;
        }
    }
}
