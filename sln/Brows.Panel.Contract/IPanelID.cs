using System;
using System.Threading;

namespace Brows {
    public interface IPanelID {
        event EventHandler ValueChanged;
        string Value { get; }
        void Begin(CancellationToken cancellationToken);
    }
}
