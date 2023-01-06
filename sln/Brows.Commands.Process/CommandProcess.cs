using Domore.Notification;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Diagnostics;

    internal class CommandProcess : Notifier {
        private ProcessDefault Default =>
            _Default ?? (
            _Default = new ProcessDefault());
        private ProcessDefault _Default;

        public IList<ProcessWrapper> List { get; } = new List<ProcessWrapper>();

        public async Task Start(string input, string workingDirectory, CancellationToken cancellationToken) {
            var i = input?.Trim() ?? "";
            if (i == "") {
                await Default.Start(workingDirectory, cancellationToken);
            }
            else {
                var process = new ProcessWrapper(i, workingDirectory);
                List.Insert(0, process);
                process.Start();
            }
        }
    }
}
