using System;

namespace Brows.Agent;

internal sealed class AgentFocusException : InvalidOperationException {
    internal long WindowId { get; }

    internal AgentFocusException(long windowId) : base(
        $"Keyboard focus is not in window {windowId}, and it could not be moved there. The input was not delivered.") {
        WindowId = windowId;
    }
}
