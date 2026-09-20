using Brows.CLI;

namespace Brows.IPC;

/// <summary>
/// The base class for commands run on an IPC host.
/// </summary>
/// <typeparam name="TParam">The type of the parameter accepted.</typeparam>
public abstract class HostCommand<TParam> : CliCommand<TParam> where TParam : new() {
}
