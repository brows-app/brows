namespace Brows {
    public sealed class WindowsProgramConfig {
        public SplashConfig Splash {
            get => _Splash ??= new();
            set => _Splash = value;
        }
        private SplashConfig _Splash;

        public sealed class SplashConfig {
            public bool Show { get; set; } = true;
        }
    }
}
