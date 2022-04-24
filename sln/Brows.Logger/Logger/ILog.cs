using System;
using System.Threading.Tasks;

namespace Brows.Logger {
    public interface ILog {
        bool Enabled(LogSeverity severity);
        void Data(LogSeverity severity, params object[] data);

        bool Critical();
        void Critical(params object[] data);

        bool Info();
        void Info(params object[] data);

        bool Error();
        void Error(params object[] data);

        bool Debug();
        void Debug(params object[] data);

        bool Warn();
        void Warn(params object[] data);

        Task Performance(string data, Func<Task> task);
    }
}
