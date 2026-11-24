using Domore.Logs;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Brows.Agent;

/*
 * Listens on a named pipe that only the current user can open. Each connection carries one request line and
 * one response line, and connections are served one at a time.
 */
internal sealed class AgentHost : IDisposable {
    private const int MaxRequestLength = 64 * 1024;

    private static readonly ILog Log = Logging.For(typeof(AgentHost));
    private static readonly TimeSpan DefaultConnectionTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource Cancellation = new();

    private bool Disposed;

    private TimeSpan ConnectionTimeout { get; }
    private Task Listening { get; }
    private string PipeName { get; }
    private Func<AgentRequest, CancellationToken, Task<AgentResponse>> Handler { get; }

    private static async Task<string> ReadLine(Stream stream, CancellationToken token) {
        var bytes = new MemoryStream();
        var buffer = new byte[4096];
        var tooLong = false;
        for (; ; ) {
            var count = await stream.ReadAsync(buffer, token);
            if (count == 0) {
                break;
            }
            var newline = Array.IndexOf(buffer, (byte)'\n', 0, count);
            var length = newline < 0 ? count : newline;
            /*
             * Keep reading a line that is too long, without keeping it, so that the client can finish
             * writing and read the response.
             */
            tooLong = tooLong || bytes.Length + length > MaxRequestLength;
            if (!tooLong) {
                bytes.Write(buffer, 0, length);
            }
            if (newline >= 0) {
                break;
            }
        }
        if (tooLong) {
            throw new InvalidDataException($"The request is longer than {MaxRequestLength} bytes.");
        }
        return Encoding.UTF8.GetString(bytes.ToArray()).TrimEnd('\r');
    }

    /*
     * Waits for the client to close its end, which it does once it has read the response. Unlike
     * WaitForPipeDrain, the wait can be cancelled.
     */
    private static async Task Closed(Stream stream, CancellationToken token) {
        var buffer = new byte[256];
        while (await stream.ReadAsync(buffer, token) > 0) {
        }
    }

    private async Task<AgentResponse> Respond(string line, CancellationToken token) {
        var request = default(AgentRequest);
        try {
            request = JsonSerializer.Deserialize<AgentRequest>(line, JsonOptions);
        }
        catch (JsonException exception) {
            return AgentResponse.Failure($"The request is not valid JSON: {exception.Message}");
        }
        if (request is null) {
            return AgentResponse.Failure("The request is empty.");
        }
        try {
            return await Handler(request, token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException) {
            if (Log.Error()) {
                Log.Error(exception);
            }
            return AgentResponse.Failure($"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private async Task<AgentResponse> Receive(NamedPipeServerStream pipe, CancellationToken token) {
        var line = default(string);
        using (var reading = CancellationTokenSource.CreateLinkedTokenSource(token)) {
            reading.CancelAfter(ConnectionTimeout);
            try {
                line = await ReadLine(pipe, reading.Token);
            }
            catch (InvalidDataException exception) {
                return AgentResponse.Failure(exception.Message);
            }
        }
        return await Respond(line, token);
    }

    private async Task Serve(NamedPipeServerStream pipe, CancellationToken token) {
        try {
            var response = await Receive(pipe, token);
            var json = JsonSerializer.Serialize(response, JsonOptions) + "\n";
            using (var delivering = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                delivering.CancelAfter(ConnectionTimeout);
                await pipe.WriteAsync(Encoding.UTF8.GetBytes(json), delivering.Token);
                await pipe.FlushAsync(delivering.Token);
                await Closed(pipe, delivering.Token);
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) {
            /*
             * The client did not send a request, or did not read the response, in time. Close the
             * connection so that the next client can be served.
             */
            if (Log.Info()) {
                Log.Info(nameof(Serve), "the connection timed out");
            }
        }
    }

    private async Task Listen(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            var connected = false;
            try {
                using (var pipe = new NamedPipeServerStream(
                    pipeName: PipeName,
                    direction: PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly)) {
                    await pipe.WaitForConnectionAsync(token);
                    connected = true;
                    await Serve(pipe, token);
                }
            }
            catch (Exception) when (token.IsCancellationRequested) {
                break;
            }
            catch (IOException exception) when (connected) {
                if (Log.Info()) {
                    Log.Info(nameof(Listen), exception.Message);
                }
            }
            catch (Exception exception) {
                if (Log.Error()) {
                    Log.Error(nameof(Listen), exception);
                }
                try {
                    await Task.Delay(RetryDelay, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) {
                    break;
                }
            }
        }
    }

    internal static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) {
        Converters = { new JsonStringEnumConverter() }
    };

    internal AgentHost(string pipeName, Func<AgentRequest, CancellationToken, Task<AgentResponse>> handler,
                       TimeSpan? connectionTimeout = null) {
        if (string.IsNullOrWhiteSpace(pipeName)) {
            throw new ArgumentException("Specify a pipe name.", nameof(pipeName));
        }
        PipeName = pipeName;
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        ConnectionTimeout = connectionTimeout ?? DefaultConnectionTimeout;
        var token = Cancellation.Token;
        Listening = Task.Run(() => Listen(token), CancellationToken.None);
    }

    public void Dispose() {
        if (Disposed) {
            return;
        }
        Disposed = true;
        Cancellation.Cancel();
        /*
         * Wait for the listener to close its pipe, so that the pipe is gone once the host is disposed.
         */
        if (Listening.Wait(StopTimeout)) {
            Cancellation.Dispose();
        }
    }
}
