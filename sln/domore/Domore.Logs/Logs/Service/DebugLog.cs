using System.Diagnostics;

namespace Domore.Logs.Service {
    internal sealed class DebugLog : ILogService {
        public void Log(string name, string data) {
            Debug.WriteLine(data, name);
        }
    }
}
