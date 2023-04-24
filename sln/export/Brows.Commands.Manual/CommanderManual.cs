using Domore.Notification;
using System;
using System.Reflection;

namespace Brows {
    internal class CommanderManual : Notifier {
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
