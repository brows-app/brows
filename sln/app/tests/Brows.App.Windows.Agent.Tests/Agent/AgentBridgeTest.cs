using Domore.Logs;
using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Brows.Agent;

[TestFixture]
[NonParallelizable]
internal sealed class AgentBridgeTest {
    private static readonly ILog Log = Logging.For(typeof(AgentBridgeTest));

    private static string PipeName() {
        return $"brows-agent-test-{Guid.NewGuid():N}";
    }

    private static async Task<AgentResponse> Request(string pipeName, string line, int timeout = 5000) {
        var options = PipeOptions.CurrentUserOnly;
        using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, options)) {
            await client.ConnectAsync(timeout);
            await client.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
            await client.FlushAsync();
            using (var reader = new StreamReader(client, Encoding.UTF8)) {
                var json = await reader.ReadLineAsync();
                return JsonSerializer.Deserialize<AgentResponse>(json, AgentHost.JsonOptions);
            }
        }
    }

    private static T Wait<T>(Task<T> task) {
        var frame = new DispatcherFrame();
        _ = task.ContinueWith(
            _ => frame.Continue = false,
            TaskScheduler.FromCurrentSynchronizationContext());
        Dispatcher.PushFrame(frame);
        return task.GetAwaiter().GetResult();
    }

    [Test]
    public void Open_ReportsEntriesLoggedBeforeItOpened() {
        DispatcherTools.Run(dispatcher => {
            var pipeName = PipeName();
            var marker = Guid.NewGuid().ToString("N");
            using (var bridge = new AgentBridge(pipeName)) {
                Log.Info(marker);
                bridge.Open(dispatcher, () => Array.Empty<Window>(), () => null);
                var response = Wait(Task.Run(() => Request(pipeName, "{\"op\":\"log\"}")));
                Assert.That(response.Ok, Is.True, response.Error);
                Assert.That(response.Log.Any(entry => entry.Text.Contains(marker)), Is.True);
            }
        });
    }

    [Test]
    public void Dispose_ClosesThePipeAndStopsTheLog() {
        DispatcherTools.Run(dispatcher => {
            var pipeName = PipeName();
            var bridge = new AgentBridge(pipeName);
            bridge.Open(dispatcher, () => Array.Empty<Window>(), () => null);
            bridge.Dispose();
            var marker = Guid.NewGuid().ToString("N");
            Log.Info(marker);
            var request = Task.Run(() => Request(pipeName, "{\"op\":\"log\"}", timeout: 300));
            Assert.That(() => Wait(request), Throws.InstanceOf<TimeoutException>());
            Assert.That(bridge.AgentLog.Since(0).Any(entry => entry.Text.Contains(marker)), Is.False);
        });
    }

    [Test]
    public void Dispose_WithoutOpen_StopsTheLog() {
        var bridge = new AgentBridge(PipeName());
        bridge.Dispose();
        var marker = Guid.NewGuid().ToString("N");
        Log.Info(marker);
        Assert.That(bridge.AgentLog.Since(0).Any(entry => entry.Text.Contains(marker)), Is.False);
    }

    [Test]
    public void Open_AfterDispose_Throws() {
        DispatcherTools.Run(dispatcher => {
            using var bridge = new AgentBridge(PipeName());
            bridge.Dispose();
            Assert.That(
                () => bridge.Open(dispatcher, () => Array.Empty<Window>(), () => null),
                Throws.InstanceOf<ObjectDisposedException>());
        });
    }

    [Test]
    public void Open_Twice_Throws() {
        DispatcherTools.Run(dispatcher => {
            using (var bridge = new AgentBridge(PipeName())) {
                bridge.Open(dispatcher, () => Array.Empty<Window>(), () => null);
                Assert.That(
                    () => bridge.Open(dispatcher, () => Array.Empty<Window>(), () => null),
                    Throws.InvalidOperationException);
            }
        });
    }
}
