namespace Brows.Terminal;

internal enum TerminalSessionState {
    NotStarted,
    Starting,
    Running,
    Stopping,
    Stopped,
    Exited,
    Failed
}
