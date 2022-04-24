using System;
using System.Reflection;

namespace Brows {
    using ComponentModel;
    using Gui;

    internal class CommanderManual : NotifyPropertyChanged, IControlled<IManualController> {
        public string Version =>
            _Version ?? (
            _Version = Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString());
        private string _Version;

        public string CopyrightYear =>
            _CopyrightYear ?? (
            _CopyrightYear = "2021-" + DateTime.Now.Year);
        private string _CopyrightYear;

        public IManualController Controller {
            get => _Controller;
            set {
                var oldValue = _Controller;
                var newValue = value;
                if (Change(ref _Controller, newValue, nameof(Controller))) {
                }
            }
        }
        private IManualController _Controller;
    }
}
