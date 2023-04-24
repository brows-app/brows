using Domore.Notification;

namespace Brows {
    using Gui;

    public sealed class CommanderTheme : Notifier {
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

        public static ICommanderTheme Service { get; set; }

        public static void Apply(CommanderTheme theme) {
            Service?.Apply(theme);
        }
    }
}
