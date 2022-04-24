using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using ComponentModel;
    using Diagnostics;
    using Threading.Tasks;

    internal class CommandProcess : NotifyPropertyChanged {
        private ProcessDefault Default =>
            _Default ?? (
            _Default = new ProcessDefault());
        private ProcessDefault _Default;

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<CommandProcess>());
        private TaskHandler _TaskHandler;

        public IList<ProcessWrapper> List { get; } = new List<ProcessWrapper>();

        public async Task Start(string input, string workingDirectory, CancellationToken cancellationToken) {
            var i = input?.Trim() ?? "";
            if (i == "") {
                await Default.Start(workingDirectory, cancellationToken);
            }
            else {
                var process = new ProcessWrapper(i, workingDirectory);
                List.Insert(0, process);
                TaskHandler.Begin(async token => {
                    await process.Run(token);
                });
            }
        }
    }
}
