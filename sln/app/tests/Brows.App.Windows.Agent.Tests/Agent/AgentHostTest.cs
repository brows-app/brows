using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Agent;

[TestFixture]
internal sealed class AgentHostTest {
    private static string PipeName() {
        return $"brows-agent-test-{Guid.NewGuid():N}";
    }

    private static Task<AgentResponse> Echo(AgentRequest request, CancellationToken token) {
        return Task.FromResult(new AgentResponse { Ok = true, Ui = request.Op });
    }

    private static async Task<string> Send(string pipeName, string line, int timeout = 5000) {
        var options = PipeOptions.CurrentUserOnly;
        using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, options)) {
            await client.ConnectAsync(timeout);
            var bytes = Encoding.UTF8.GetBytes(line + "\n");
            await client.WriteAsync(bytes);
            await client.FlushAsync();
            using (var reader = new StreamReader(client, Encoding.UTF8)) {
                return await reader.ReadLineAsync();
            }
        }
    }

    private static async Task<AgentResponse> Request(string pipeName, string line) {
        var json = await Send(pipeName, line);
        return JsonSerializer.Deserialize<AgentResponse>(json, AgentHost.JsonOptions);
    }

    [Test]
    public async Task Request_IsHandledAndAnswered() {
        var pipeName = PipeName();
        using (new AgentHost(pipeName, Echo)) {
            var response = await Request(pipeName, "{\"op\":\"echo\"}");
            Assert.That(response.Ok, Is.True);
            Assert.That(response.Ui, Is.EqualTo("echo"));
            Assert.That(response.Pid, Is.EqualTo(Environment.ProcessId));
        }
    }

    [Test]
    public async Task Request_InvalidJson_FailsWithoutCallingTheHandler() {
        var pipeName = PipeName();
        var calls = 0;
        Task<AgentResponse> handler(AgentRequest request, CancellationToken token) {
            calls++;
            return Echo(request, token);
        }
        using (new AgentHost(pipeName, handler)) {
            var response = await Request(pipeName, "not json");
            Assert.That(response.Ok, Is.False);
            Assert.That(response.Error, Does.Contain("not valid JSON"));
            Assert.That(calls, Is.Zero);
        }
    }

    [Test]
    public async Task Request_TooLong_Fails() {
        var pipeName = PipeName();
        using (new AgentHost(pipeName, Echo)) {
            var text = new string('x', 70 * 1024);
            var response = await Request(pipeName, $"{{\"op\":\"type\",\"text\":\"{text}\"}}");
            Assert.That(response.Ok, Is.False);
            Assert.That(response.Error, Does.Contain("longer than"));
        }
    }

    [Test]
    public async Task Requests_AreServedOneAfterAnother() {
        var pipeName = PipeName();
        using (new AgentHost(pipeName, Echo)) {
            var first = await Request(pipeName, "{\"op\":\"first\"}");
            var second = await Request(pipeName, "{\"op\":\"second\"}");
            Assert.That(first.Ui, Is.EqualTo("first"));
            Assert.That(second.Ui, Is.EqualTo("second"));
        }
    }

    [Test]
    public async Task Response_WritesNullFields() {
        var pipeName = PipeName();
        using (new AgentHost(pipeName, Echo)) {
            var json = await Send(pipeName, "{\"op\":\"echo\"}");
            using (var document = JsonDocument.Parse(json)) {
                Assert.That(document.RootElement.TryGetProperty("focus", out var focus), Is.True);
                Assert.That(focus.ValueKind, Is.EqualTo(JsonValueKind.Null));
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Request_AfterAClientStalls_IsServed(bool stallAfterWriting) {
        var pipeName = PipeName();
        using (new AgentHost(pipeName, Echo, connectionTimeout: TimeSpan.FromMilliseconds(500))) {
            var options = PipeOptions.CurrentUserOnly;
            using (var stalled = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, options)) {
                await stalled.ConnectAsync(5000);
                if (stallAfterWriting) {
                    /*
                     * Write a request, but never read the response.
                     */
                    await stalled.WriteAsync(Encoding.UTF8.GetBytes("{\"op\":\"stall\"}\n"));
                    await stalled.FlushAsync();
                }
                var served = Request(pipeName, "{\"op\":\"after\"}");
                var finished = await Task.WhenAny(served, Task.Delay(TimeSpan.FromSeconds(10)));
                Assert.That(finished, Is.SameAs(served), "The second client was not served.");
                Assert.That((await served).Ui, Is.EqualTo("after"));
            }
        }
    }

    [Test]
    [NonParallelizable]
    public async Task Listen_WhenPipeNameIsBusy_SpacesRetriesAndServesAfterRelease() {
        var pipeName = PipeName();
        using var log = new AgentLog();
        log.Subscribe();
        var failures = Array.Empty<AgentLogEntry>();
        using var blocker = new NamedPipeServerStream(
            pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var host = new AgentHost(pipeName, Echo);
        var observedRetries = SpinWait.SpinUntil(() => {
            failures = log.Since(0)
                .Where(entry => entry.Name.EndsWith(nameof(AgentHost), StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            return failures.Length == 2;
        }, TimeSpan.FromSeconds(5));
        Assert.That(observedRetries, Is.True, "The host did not retry the busy pipe name.");
        Assert.That(failures[1].Time - failures[0].Time, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(750)));
        blocker.Dispose();
        var response = await Request(pipeName, "{\"op\":\"after-release\"}");
        Assert.That(response.Ui, Is.EqualTo("after-release"));
    }

    [Test]
    public void Dispose_ClosesThePipe() {
        var pipeName = PipeName();
        var host = new AgentHost(pipeName, Echo);
        host.Dispose();
        Assert.That(
            async () => await Send(pipeName, "{\"op\":\"echo\"}", timeout: 300),
            Throws.InstanceOf<TimeoutException>());
    }
}
