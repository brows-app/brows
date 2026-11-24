using Brows.Localization;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Brows.Agent;

/*
 * Lets an agent drive the application through a named pipe. The bridge runs only in Debug builds, and only
 * when the program is started with agent.pipe=<name>, whose value is passed here as the pipe name.
 *
 * It starts in two phases. It is created, and its log subscribed, before the program discovers its exports,
 * so that the log holds what discovery reports. It is opened once the application exists, which starts the
 * pipe. Disposing it closes the pipe and then unsubscribes the log.
 */
/// <summary>
/// Lets an agent drive the application through a named pipe that only the current user can open.
/// </summary>
public sealed class AgentBridge : IDisposable {
    private static readonly ILog Log = Logging.For(typeof(AgentBridge));

    private readonly Lock Lifetime = new();

    private AgentHost Host;
    private bool Disposed;

    private string PipeName { get; }

    private static string Pipe(string pipeName) {
        var pipe = pipeName?.Trim();
        return string.IsNullOrEmpty(pipe)
            ? null
            : pipe;
    }

    internal AgentLog AgentLog { get; }

    internal AgentBridge(string pipeName) {
        if (string.IsNullOrWhiteSpace(pipeName)) {
            throw new ArgumentException("Specify a pipe name.", nameof(pipeName));
        }
        PipeName = pipeName;
        AgentLog = new AgentLog();
        AgentLog.Subscribe();
    }

    /// <summary>
    /// Determines whether a pipe name asks for an agent session.
    /// </summary>
    /// <param name="pipeName">
    /// The value of <c>agent.pipe</c> from the command line, or null.
    /// </param>
    /// <returns>
    /// True if the pipe name is not null, empty, or white space; otherwise, false.
    /// </returns>
    public static bool Requested(string pipeName) {
        return Pipe(pipeName) is not null;
    }

    /// <summary>
    /// Creates a bridge and subscribes its log, so that the log holds what is logged from then on. Create the
    /// bridge before the program discovers its exports, and open it once the application exists.
    /// </summary>
    /// <param name="pipeName">
    /// The value of <c>agent.pipe</c> from the command line, or null.
    /// </param>
    /// <returns>
    /// The new bridge, or null if the pipe name does not ask for an agent session or the build is not a Debug
    /// build.
    /// </returns>
    public static AgentBridge Create(string pipeName) {
#if DEBUG
        var pipe = Pipe(pipeName);
        return pipe is null
            ? null
            : new AgentBridge(pipe);
#else
        return null;
#endif
    }

    /// <summary>
    /// Starts listening on the bridge's pipe and serving agent requests against the application's windows.
    /// </summary>
    /// <param name="dispatcher">
    /// The dispatcher that owns the windows.
    /// </param>
    /// <param name="windows">
    /// Returns the windows that agents may drive. It is called on the dispatcher.
    /// </param>
    /// <param name="translation">
    /// Returns the translation used to read key gestures, or null while it is not yet available.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="dispatcher"/>, <paramref name="windows"/>, or <paramref name="translation"/>
    /// is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the bridge is already open.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the bridge has been disposed.
    /// </exception>
    public void Open(Dispatcher dispatcher, Func<IReadOnlyList<Window>> windows, Func<ITranslation> translation) {
        lock (Lifetime) {
            ObjectDisposedException.ThrowIf(Disposed, this);
            if (Host is not null) {
                throw new InvalidOperationException("The bridge is already open.");
            }
            var operations = new AgentOperations(dispatcher, windows, translation, AgentLog);
            Host = new AgentHost(PipeName, operations.Handle);
        }
        if (Log.Info()) {
            Log.Info(nameof(Open), PipeName);
        }
    }

    /// <summary>
    /// Closes the bridge's pipe, if it is open, and then unsubscribes its log.
    /// </summary>
    public void Dispose() {
        AgentHost host;
        lock (Lifetime) {
            if (Disposed) {
                return;
            }
            Disposed = true;
            host = Host;
        }
        try {
            host?.Dispose();
        }
        finally {
            AgentLog.Dispose();
        }
    }
}
