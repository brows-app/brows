using System;

namespace Domore.Logs {
    using Threading.Channels;

    internal class LogWork : ChannelWork<LogEntry> {
        public LogWork(string name, Action<LogEntry> action) : base(name, action) {
        }
    }
}
