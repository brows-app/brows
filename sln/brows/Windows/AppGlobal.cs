using System;

namespace Brows.Windows {
    using Translation;

    internal class AppGlobal {
        public App App { get; }

        public AppGlobal(App app) {
            App = app ?? throw new ArgumentNullException(nameof(app));
            Global.Translation = App.Instance.Components.Translation();
        }
    }
}
