using System;

namespace Brows.Terminal;

internal sealed class TerminalSessionLifecycleEventArgs : EventArgs {
    internal long OperationId { get; }

    internal long FailureId { get; }

    internal TerminalSessionState State { get; }

    internal uint? ExitCode { get; }

    internal Exception Exception { get; }

    internal TerminalSessionLifecycleEventArgs(long operationId,
                                               long failureId,
                                               TerminalSessionState state,
                                               uint? exitCode,
                                               Exception exception) {
        OperationId = operationId;
        FailureId = failureId;
        State = state;
        ExitCode = exitCode;
        Exception = exception;
    }
}
