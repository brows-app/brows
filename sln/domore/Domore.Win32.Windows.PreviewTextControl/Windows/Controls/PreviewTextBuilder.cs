using Domore.Text;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Domore.Windows.Controls {
    internal sealed class PreviewTextBuilder : DecodedTextBuilder {
        public PreviewTextControl Control { get; }

        public PreviewTextBuilder(PreviewTextControl control) {
            Control = control ?? throw new ArgumentNullException(nameof(control));
        }

        protected override sealed async Task Clear(CancellationToken cancellationToken) {
            await Control.Dispatcher.InvokeAsync(cancellationToken: cancellationToken, priority: DispatcherPriority.Render, callback: () => {
                Control.TextBox.Clear();
            });
        }

        protected override sealed async Task Add(ReadOnlyMemory<char> memory, CancellationToken cancellationToken) {
            var s = new string(memory.Span);
            await Control.Dispatcher.InvokeAsync(cancellationToken: cancellationToken, priority: DispatcherPriority.Render, callback: () => {
                Control.TextBox.AppendText(s);
            });
        }
    }
}
