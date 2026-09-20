using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Brows.IPC.MessageSubscriptions;

internal sealed class HostSubscription : CommanderMessageSubscription {
    private static readonly ILog Log = Logging.For(typeof(HostSubscription));

    private readonly Guid Guid;
    private readonly Task HostTask;
    private readonly ObjectCallHost Host;
    private readonly CancellationTokenSource TokenSource;
    private readonly Channel<CommanderMessage> Ch;

    private bool Reading;

    private async Task HostTaskFactory(CancellationToken token) {
        try {
            await Host.Run(token: token, info: new() {
                OnStart = async (state, token) => {
                    Directory.CreateDirectory(PortDirectory);
                    await File.WriteAllTextAsync(PortPath, state?.Port.ToString(), token);
                }
            });
        }
        finally {
            try {
                File.Delete(PortPath);
            }
            catch (Exception ex) {
                Log.Error(ex);
            }
        }
    }

    internal static string PortDirectory => field ??= Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify),
            "Brows",
            "ipc");

    internal static string PortPath => field ??= Path.Combine(
            PortDirectory,
            "port");

    internal HostSubscription() {
        Ch = Channel.CreateUnbounded<CommanderMessage>(new() {
            SingleReader = true
        });
        Guid = Guid.NewGuid();
        TokenSource = new();
        Host = new(new Delegate(Ch.Writer));
        HostTask = HostTaskFactory(TokenSource.Token);
    }

    protected sealed override void Dispose(bool disposing) {
        if (disposing) {
            using (TokenSource) {
                var alreadyDisposed = false;
                try {
                    TokenSource.Cancel();
                }
                catch (ObjectDisposedException) {
                    alreadyDisposed = true;
                }
                catch (Exception ex) {
                    Log.Warn(ex);
                }
                if (!alreadyDisposed) {
                    _ = Task.Run(async () => {
                        try {
                            await HostTask;
                        }
                        catch (OperationCanceledException) {
                            Log.Debug("Host task canceled");
                        }
                        catch (Exception ex) {
                            Log.Error("Host task finished with error",
                                      ex);
                        }
                    });
                }
            }
        }
        base.Dispose(disposing);
    }

    public sealed override async
    IAsyncEnumerable<CommanderMessage> Messages([EnumeratorCancellation] CancellationToken token) {
        lock (Ch) {
            /*
             * We guaranteed only a single reader in the construction
             * of the channel, so we need to prevent reentrancy, here.
             */
            Reading = Reading
                ? throw new InvalidOperationException("Already reading")
                : true;
        }
        try {
            using (var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(token, TokenSource.Token)) {
                var reader = Ch.Reader;
                var linkedToken = linkedSource.Token;
                for (; ; ) {
                    var read = false;
                    try {
                        read = await reader.WaitToReadAsync(linkedToken);
                    }
                    catch (OperationCanceledException) when (linkedToken.IsCancellationRequested) {
                        /*
                         * Only re-throw this if the token passed to the method is requesting it.
                         * Otherwise, it may be our private token. If that's the case, just exit.
                         */
                        if (token.IsCancellationRequested) {
                            throw;
                        }
                        break;
                    }
                    if (read == false) {
                        break;
                    }
                    if (reader.TryRead(out var item)) {
                        yield return item;
                    }
                }
            }
        }
        finally {
            Reading = false;
        }
    }

    private sealed class Delegate : IObjectCallDelegate {
        IObjectTypeResolver IObjectCallDelegate.TypeResolver { get; } =
            new CommanderMessageTypeResolver();

        async Task<object> IObjectCallDelegate.Call(object input, CancellationToken token) {
            if (input is CommanderMessage data) {
                var writer = Writer;
                var writtenQuickly = writer.TryWrite(data);
                if (writtenQuickly != true) {
                    for (; ; ) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        var writable = await writer.WaitToWriteAsync(token);
                        if (writable != true) {
                            break;
                        }
                        var writtenSlowly = writer.TryWrite(data);
                        if (writtenSlowly) {
                            break;
                        }
                    }
                }
            }
            return null;
        }

        public ChannelWriter<CommanderMessage> Writer { get; }

        public Delegate(ChannelWriter<CommanderMessage> writer) {
            Writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }
    }
}
