using System;
using System.Reflection;

namespace Brows {
    using ComponentModel;

    internal class CommanderManual : NotifyPropertyChanged {
        public string Version =>
            _Version ?? (
            _Version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        private string _Version;

        public string CopyrightYear =>
            _CopyrightYear ?? (
            _CopyrightYear = "2021-" + DateTime.Now.Year);
        private string _CopyrightYear;
    }
}
