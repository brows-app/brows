using System;

namespace Brows.Windows {
    using Translation;

    internal class AppGlobal {
        public WindowsApplication App { get; }

        public AppGlobal(WindowsApplication app) {
            App = app ?? throw new ArgumentNullException(nameof(app));
            Global.Translation = App.Service.Components.Translation();
        }
    }
}
